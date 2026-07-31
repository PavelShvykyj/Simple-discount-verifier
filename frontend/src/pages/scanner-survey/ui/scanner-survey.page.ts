import { HttpClient } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { form, type FieldTree } from '@angular/forms/signals';
import {
  IonBackButton,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonCol,
  IonContent,
  IonGrid,
  IonHeader,
  IonInput,
  IonNote,
  IonRow,
  IonSelect,
  IonSelectOption,
  IonSpinner,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import * as bwipjs from '@bwip-js/browser';

import { SupportQrScannerComponent } from './support-qr-scanner.component';

type BranchName = 'Люксор' | 'Дастор' | 'Вопак';
type ReadabilityAnswer = 'yes' | 'no' | null;
type QrTestStatus = 'idle' | 'scanned';
type SubmitStatus = 'idle' | 'submitting' | 'success' | 'error';
type ControlKey = 'branchName' | 'terminalName' | `answer:${string}`;

interface BarcodeScenario {
  readonly id: string;
  readonly title: string;
  readonly formatLabel: string;
  readonly bcid: 'code128' | 'ean13';
  readonly value: string;
  readonly purpose: string;
}

interface TerminalSurvey {
  readonly id: number;
  terminalName: string;
  answers: Record<string, ReadabilityAnswer>;
}

interface ScannerSurveyFormModel {
  branchName: BranchName;
  terminals: TerminalSurvey[];
}

interface ScannerSurveyPayload {
  branchName: string;
  submittedAtClient: string;
  terminals: {
    terminalName: string;
    answers: {
      barcodeId: string;
      isReadable: boolean | null;
    }[];
  }[];
}

const BRANCH_OPTIONS: readonly BranchName[] = ['Люксор', 'Дастор', 'Вопак'];
const SUPPORT_QR_STEP_TITLE = 'QR підтримки камерою';
const SUPPORT_QR_VALUE_PREFIX = 'SDV-SUPPORT-QR:';

const BARCODE_SCENARIOS: readonly BarcodeScenario[] = [
  {
    id: 'code128-web-prefixed',
    title: 'Вебкод з текстовим префіксом',
    formatLabel: 'Code 128',
    bcid: 'code128',
    value: 'SDV-BR01-TERM01-A7K9',
    purpose: 'Перевіряє майбутній формат коду застосунку, який має відрізнятися від EAN-13.',
  },
  {
    id: 'code128-numeric',
    title: 'Тільки цифри у Code 128',
    formatLabel: 'Code 128',
    bcid: 'code128',
    value: '240613000001',
    purpose: 'Показує, як сканер поводиться з числовим значенням у Code 128.',
  },
  {
    id: 'code128-long',
    title: 'Довший службовий код',
    formatLabel: 'Code 128',
    bcid: 'code128',
    value: 'SDV-DISCOUNT-20260601-000123',
    purpose: 'Дає більшу ширину штрихкоду для перевірки екранів і дистанції сканування.',
  },
  {
    id: 'ean13-common',
    title: 'Типовий товарний EAN-13',
    formatLabel: 'EAN-13',
    bcid: 'ean13',
    value: '5901234123457',
    purpose: 'Базовий контроль для сканерів, які вже читають товарні коди.',
  },
  {
    id: 'ean13-ua-prefix',
    title: 'EAN-13 з українським префіксом',
    formatLabel: 'EAN-13',
    bcid: 'ean13',
    value: '4821234567895',
    purpose: 'Перевіряє звичний сценарій для локальних товарних штрихкодів.',
  },
];

@Component({
  selector: 'app-scanner-survey-page',
  imports: [
    IonBackButton,
    IonButton,
    IonButtons,
    IonCard,
    IonCardContent,
    IonCardHeader,
    IonCardSubtitle,
    IonCardTitle,
    IonCol,
    IonContent,
    IonGrid,
    IonHeader,
    IonInput,
    IonNote,
    IonRow,
    IonSelect,
    IonSelectOption,
    IonSpinner,
    IonTitle,
    IonToolbar,
    SupportQrScannerComponent,
  ],
  templateUrl: './scanner-survey.page.html',
  styleUrl: './scanner-survey.page.scss',
})
export class ScannerSurveyPage {
  private readonly http = inject(HttpClient);

  protected readonly branchOptions = BRANCH_OPTIONS;
  protected readonly barcodes = BARCODE_SCENARIOS;
  protected readonly barcodeImages = this.createBarcodeImages();
  protected readonly currentStepIndex = signal(0);
  protected readonly touchedControls = signal<ReadonlySet<ControlKey>>(new Set());
  protected readonly dirtyControls = signal<ReadonlySet<ControlKey>>(new Set());
  protected readonly submitStatus = signal<SubmitStatus>('idle');
  protected readonly supportQrValue = signal(this.createSupportQrValue());
  protected readonly supportQrStatus = signal<QrTestStatus>('idle');
  protected readonly scannedSupportQrValue = signal<string | null>(null);
  protected readonly isQrScannerRequested = signal(false);
  protected readonly surveyModel = signal<ScannerSurveyFormModel>(this.createInitialModel('Люксор'));
  protected readonly surveyForm: FieldTree<ScannerSurveyFormModel> = form(this.surveyModel);
  protected readonly formValue = computed(() => this.surveyForm().value());
  protected readonly branchRequiredError = computed(() => !this.isBranchName(this.formValue().branchName));
  protected readonly terminalNameRequiredError = computed(() => this.terminal().terminalName.trim().length === 0);
  protected readonly isFormValid = computed(
    () =>
      !this.branchRequiredError() &&
      !this.terminalNameRequiredError() &&
      this.barcodes.every((barcode) => this.terminal().answers[barcode.id] !== null),
  );
  protected readonly steps = computed(() => [
    SUPPORT_QR_STEP_TITLE,
    'Робоче місце',
    ...this.barcodes.map((barcode) => barcode.title),
    'Відправка',
  ]);
  protected readonly currentStep = computed(() => this.steps()[this.currentStepIndex()] ?? this.steps()[0]);
  protected readonly isFirstStep = computed(() => this.currentStepIndex() === 0);
  protected readonly isLastStep = computed(() => this.currentStepIndex() === this.steps().length - 1);
  protected readonly isWorkplaceStep = computed(() => this.currentStepIndex() === this.workplaceStepIndex());
  protected readonly activeBarcode = computed(() =>
    this.currentStepIndex() > this.workplaceStepIndex() &&
    this.currentStepIndex() <= this.barcodes.length + this.workplaceStepIndex()
      ? this.barcodes[this.currentStepIndex() - this.workplaceStepIndex() - 1]
      : undefined,
  );
  protected readonly isSupportQrStep = computed(
    () => this.currentStepIndex() === this.supportQrStepIndex(),
  );
  protected readonly terminal = computed(() => this.formValue().terminals[0]);
  protected readonly supportQrImage = computed(() =>
    this.svgToDataUrl(
      bwipjs.toSVG({
        bcid: 'qrcode',
        text: this.supportQrValue(),
        scale: 6,
        paddingwidth: 8,
        paddingheight: 8,
        backgroundcolor: 'FFFFFF',
      }),
    ),
  );

  constructor() {
    effect(() => {
      if (!this.isSupportQrStep()) {
        this.isQrScannerRequested.set(false);
      }
    });
  }

  protected setBranchName(value: string): void {
    if (this.isBranchName(value)) {
      if (value !== this.formValue().branchName) {
        this.markControlDirty('branchName');
      }

      this.patchModel({ branchName: value });
    }
  }

  protected markBranchTouched(): void {
    this.markControlTouched('branchName');
  }

  protected setTerminalName(value: string): void {
    if (value !== this.terminal().terminalName) {
      this.markControlDirty('terminalName');
    }

    this.updateTerminal({ terminalName: value });
  }

  protected markTerminalNameTouched(): void {
    this.markControlTouched('terminalName');
  }

  protected moveTerminalNameFocusForward(event: Event): void {
    event.preventDefault();
    this.markTerminalNameTouched();
    this.blurEventTarget(event);
    this.focusElementById('scanner-survey-next-action');
  }

  protected setAnswer(barcodeId: string, value: string): void {
    if (value !== 'yes' && value !== 'no') {
      return;
    }

    const terminal = this.terminal();
    const controlKey = this.answerControlKey(barcodeId);

    this.markControlTouched(controlKey);

    if (terminal.answers[barcodeId] !== value) {
      this.markControlDirty(controlKey);
    }

    this.updateTerminal({
      answers: {
        ...terminal.answers,
        [barcodeId]: value,
      },
    });
  }

  protected previousStep(): void {
    this.currentStepIndex.update((index) => Math.max(index - 1, 0));
  }

  protected nextStep(): void {
    if (!this.isStepValid(this.currentStepIndex())) {
      return;
    }

    this.currentStepIndex.update((index) => Math.min(index + 1, this.steps().length - 1));
  }

  protected setCurrentStep(index: number): void {
    if (!this.canNavigateToStep(index)) {
      return;
    }

    this.currentStepIndex.set(index);
  }

  protected submitSurvey(): void {
    if (!this.isFormValid()) {
      return;
    }

    if (this.submitStatus() === 'submitting') {
      return;
    }

    this.submitStatus.set('submitting');

    this.http.post('/api/scanner-survey', this.createPayload()).subscribe({
      next: () => {
        const branchName = this.surveyModel().branchName;
        this.surveyModel.set(this.createInitialModel(branchName));
        this.currentStepIndex.set(0);
        this.resetSupportQrTest();
        this.touchedControls.set(new Set());
        this.dirtyControls.set(new Set());
        this.submitStatus.set('success');
      },
      error: () => {
        this.submitStatus.set('error');
      },
    });
  }

  protected barcodeImage(barcodeId: string): string {
    return this.barcodeImages.get(barcodeId) ?? '';
  }

  protected requestQrScanner(): void {
    this.isQrScannerRequested.set(true);
  }

  protected refreshSupportQr(): void {
    this.supportQrValue.set(this.createSupportQrValue());
    this.supportQrStatus.set('idle');
    this.scannedSupportQrValue.set(null);
  }

  protected confirmSupportQrScan(value: string): void {
    this.scannedSupportQrValue.set(value);
    this.supportQrStatus.set('scanned');
    this.isQrScannerRequested.set(false);
  }

  protected isStepValid(index: number): boolean {
    if (index === this.supportQrStepIndex()) {
      return true;
    }

    if (index === this.workplaceStepIndex()) {
      return !this.terminalNameRequiredError();
    }

    if (this.isBarcodeStepIndex(index)) {
      return this.terminal().answers[this.barcodes[index - this.workplaceStepIndex() - 1].id] !== null;
    }

    return this.isFormValid();
  }

  protected shouldShowBranchError(): boolean {
    return this.branchRequiredError() && this.isControlTouchedAndDirty('branchName');
  }

  protected shouldShowTerminalError(): boolean {
    return this.terminalNameRequiredError() && this.isControlTouchedAndDirty('terminalName');
  }

  protected shouldShowAnswerError(barcodeId: string): boolean {
    return (
      this.terminal().answers[barcodeId] === null &&
      this.isControlTouchedAndDirty(this.answerControlKey(barcodeId))
    );
  }

  protected shouldShowStepError(index: number): boolean {
    if (index === this.workplaceStepIndex()) {
      return this.shouldShowTerminalError();
    }

    if (this.isBarcodeStepIndex(index)) {
      return this.shouldShowAnswerError(this.barcodes[index - this.workplaceStepIndex() - 1].id);
    }

    if (index === this.supportQrStepIndex()) {
      return this.isQrScannerRequested() && this.supportQrStatus() !== 'scanned';
    }

    return false;
  }

  protected canNavigateToStep(index: number): boolean {
    if (index === this.supportQrStepIndex()) {
      return true;
    }

    if (index <= this.currentStepIndex()) {
      return true;
    }

    return this.steps()
      .slice(0, index)
      .every((_, stepIndex) => stepIndex === this.supportQrStepIndex() || this.isStepValid(stepIndex));
  }

  private createInitialModel(branchName: BranchName): ScannerSurveyFormModel {
    return {
      branchName,
      terminals: [this.createTerminal(1)],
    };
  }

  private createTerminal(id: number): TerminalSurvey {
    return {
      id,
      terminalName: '',
      answers: Object.fromEntries(this.barcodes.map((barcode) => [barcode.id, null])),
    };
  }

  private patchModel(patch: Partial<ScannerSurveyFormModel>): void {
    this.surveyModel.update((model) => ({
      ...model,
      ...patch,
    }));
  }

  private updateTerminal(patch: Partial<TerminalSurvey>): void {
    const terminal = this.terminal();

    this.patchModel({
      terminals: [
        {
          ...terminal,
          ...patch,
        },
      ],
    });
  }

  private markControlTouched(controlKey: ControlKey): void {
    this.touchedControls.update((controls) => new Set(controls).add(controlKey));
  }

  private markControlDirty(controlKey: ControlKey): void {
    this.dirtyControls.update((controls) => new Set(controls).add(controlKey));
  }

  private isControlTouchedAndDirty(controlKey: ControlKey): boolean {
    return this.touchedControls().has(controlKey) && this.dirtyControls().has(controlKey);
  }

  private blurEventTarget(event: Event): void {
    if (event.target instanceof HTMLElement) {
      event.target.blur();
    }
  }

  private focusElementById(elementId: string): void {
    requestAnimationFrame(() => document.getElementById(elementId)?.focus());
  }

  private answerControlKey(barcodeId: string): ControlKey {
    return `answer:${barcodeId}`;
  }

  private supportQrStepIndex(): number {
    return 0;
  }

  private workplaceStepIndex(): number {
    return 1;
  }

  private isBarcodeStepIndex(index: number): boolean {
    return index > this.workplaceStepIndex() && index <= this.barcodes.length + this.workplaceStepIndex();
  }

  private resetSupportQrTest(): void {
    this.supportQrValue.set(this.createSupportQrValue());
    this.supportQrStatus.set('idle');
    this.scannedSupportQrValue.set(null);
    this.isQrScannerRequested.set(false);
  }

  private createPayload(): ScannerSurveyPayload {
    const model = this.surveyModel();

    return {
      branchName: model.branchName,
      submittedAtClient: new Date().toISOString(),
      terminals: model.terminals.map((terminal) => ({
        terminalName: terminal.terminalName,
        answers: this.barcodes.map((barcode) => ({
          barcodeId: barcode.id,
          isReadable: terminal.answers[barcode.id] === null ? null : terminal.answers[barcode.id] === 'yes',
        })),
      })),
    };
  }

  private createBarcodeImages(): ReadonlyMap<string, string> {
    return new Map(
      this.barcodes.map((barcode) => [
        barcode.id,
        this.svgToDataUrl(
          bwipjs.toSVG({
            bcid: barcode.bcid,
            text: barcode.value,
            scale: 2,
            height: barcode.bcid === 'ean13' ? 18 : 16,
            includetext: true,
            textxalign: 'center',
            paddingwidth: 10,
            paddingheight: 6,
            backgroundcolor: 'FFFFFF',
          }),
        ),
      ]),
    );
  }

  private svgToDataUrl(svg: string): string {
    return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
  }

  private createSupportQrValue(): string {
    return `${SUPPORT_QR_VALUE_PREFIX}${crypto.randomUUID()}`;
  }

  private isBranchName(value: string): value is BranchName {
    return BRANCH_OPTIONS.includes(value as BranchName);
  }
}
