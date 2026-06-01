import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
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
  IonContent,
  IonHeader,
  IonNote,
  IonSelect,
  IonSelectOption,
  IonTitle,
  IonToolbar,
} from '@ionic/angular/standalone';
import * as bwipjs from '@bwip-js/browser';

import { ThemeModeToggleComponent } from '../../../shared/theme/ui/theme-mode-toggle.component';

type BranchName = 'Люксор' | 'Дастор' | 'Вопак';
type ReadabilityAnswer = 'yes' | 'no' | null;
type SubmitStatus = 'idle' | 'submitting' | 'success' | 'error';

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
    IonContent,
    IonHeader,
    IonNote,
    IonSelect,
    IonSelectOption,
    IonTitle,
    IonToolbar,
    ThemeModeToggleComponent,
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
  protected readonly submitStatus = signal<SubmitStatus>('idle');
  protected readonly surveyModel = signal<ScannerSurveyFormModel>(this.createInitialModel('Люксор'));
  protected readonly surveyForm: FieldTree<ScannerSurveyFormModel> = form(this.surveyModel);
  protected readonly formValue = computed(() => this.surveyForm().value());
  protected readonly steps = computed(() => [
    'Робоче місце',
    ...this.barcodes.map((barcode) => barcode.title),
    'Відправка',
  ]);
  protected readonly currentStep = computed(() => this.steps()[this.currentStepIndex()] ?? this.steps()[0]);
  protected readonly isFirstStep = computed(() => this.currentStepIndex() === 0);
  protected readonly isLastStep = computed(() => this.currentStepIndex() === this.steps().length - 1);
  protected readonly activeBarcode = computed(() =>
    this.currentStepIndex() > 0 && !this.isLastStep()
      ? this.barcodes[this.currentStepIndex() - 1]
      : undefined,
  );
  protected readonly terminal = computed(() => this.formValue().terminals[0]);

  protected setBranchName(value: string): void {
    if (this.isBranchName(value)) {
      this.patchModel({ branchName: value });
    }
  }

  protected setTerminalName(value: string): void {
    this.updateTerminal({ terminalName: value });
  }

  protected setAnswer(barcodeId: string, value: string): void {
    if (value !== 'yes' && value !== 'no') {
      return;
    }

    const terminal = this.terminal();

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
    this.currentStepIndex.update((index) => Math.min(index + 1, this.steps().length - 1));
  }

  protected submitSurvey(): void {
    if (this.submitStatus() === 'submitting') {
      return;
    }

    this.submitStatus.set('submitting');

    this.http.post('/api/scanner-survey', this.createPayload()).subscribe({
      next: () => {
        const branchName = this.surveyModel().branchName;
        this.surveyModel.set(this.createInitialModel(branchName));
        this.currentStepIndex.set(0);
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

  private isBranchName(value: string): value is BranchName {
    return BRANCH_OPTIONS.includes(value as BranchName);
  }
}
