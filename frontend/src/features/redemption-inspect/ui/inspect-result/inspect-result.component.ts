import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import {
  IonBadge,
  IonItem,
  IonLabel,
  IonList,
  IonNote,
} from '@ionic/angular/standalone';

import { RedemptionInspectResponse } from '../../../../entities/redemption-inspect/model/redemption-inspect.types';

interface AuditEventView {
  readonly eventType: string;
  readonly occurredAt: string;
  readonly actorText: string;
}

const REDEMPTION_STATUS_LABELS: Record<string, string> = {
  started: 'Звернення створено',
  profile_not_found: 'Анкету не знайдено',
  sms_send_failed: 'SMS не надіслано',
  sms_sent: 'SMS надіслано',
  sms_failed: 'SMS не підтверджено',
  barcode_issued: 'Штрихкод видано',
  barcode_consumed: 'Знижку використано',
  barcode_expired: 'Штрихкод прострочений',
  unknown: 'Стан невідомий',
};

const BARCODE_STATUS_LABELS: Record<string, string> = {
  active: 'Активний',
  expired: 'Прострочений',
  consumed: 'Використаний',
  replaced_by_new_flow: 'Замінений новим кодом',
  invalid_format: 'Неправильний код',
  not_issued: 'Ще не видано',
  unknown: 'Стан невідомий',
};

const EVENT_LABELS: Record<string, string> = {
  customer_profile_created: 'Анкету клієнта створено',
  customer_profile_updated: 'Анкету клієнта оновлено',
  redemption_started: 'Клієнт почав отримання знижки',
  invalid_phone: 'Телефон введено неправильно',
  profile_not_found: 'Анкету за телефоном не знайдено',
  profile_found: 'Анкету знайдено',
  sms_send_requested: 'Запитано SMS',
  sms_sent: 'SMS надіслано',
  sms_send_failed: 'SMS не вдалося надіслати',
  sms_validation_failed: 'SMS-код введено неправильно',
  phone_verified: 'Телефон підтверджено',
  previous_barcode_invalidated: 'Попередній штрихкод скасовано',
  barcode_issued: 'Штрихкод видано',
  barcode_validation_requested: 'Каса перевірила штрихкод',
  barcode_validation_failed: 'Каса відхилила штрихкод',
  barcode_validation_succeeded: 'Каса прийняла штрихкод',
  barcode_validation_idempotent_replay: 'Каса повторила успішну перевірку',
  barcode_consumed: 'Штрихкод використано',
  barcode_expired: 'Штрихкод прострочено',
  pos_profile_requested: 'Каса запросила анкету',
  pos_profile_returned: 'Анкету передано в касу',
  pos_profile_not_found: 'Анкету для каси не знайдено',
};

@Component({
  selector: 'app-inspect-result',
  imports: [DatePipe, IonBadge, IonItem, IonLabel, IonList, IonNote],
  templateUrl: './inspect-result.component.html',
  styleUrl: './inspect-result.component.scss',
})
export class InspectResultComponent {
  readonly result = input.required<RedemptionInspectResponse>();

  protected readonly profileTitle = computed(() => {
    const profile = this.result().profile;

    if (profile === null || profile === undefined) {
      return 'Анкета не знайдена';
    }

    return (
      profile.answers.find((answer) => answer.code === 'fullName')?.value ??
      'Анкета без ПІБ'
    );
  });

  protected readonly hasAuditEvents = computed(() => this.result().auditEvents.length > 0);
  protected readonly redemptionStatusText = computed(
    () =>
      REDEMPTION_STATUS_LABELS[this.result().redemption.status] ??
      this.result().redemption.status,
  );
  protected readonly barcodeStatusText = computed(() => {
    const barcode = this.result().barcode;

    if (barcode === null || barcode === undefined) {
      return 'Немає даних';
    }

    return BARCODE_STATUS_LABELS[barcode.status] ?? barcode.status;
  });
  protected readonly auditEventViews = computed<readonly AuditEventView[]>(() =>
    this.result().auditEvents.map((event) => {
      const actorParts = [event.actorType, event.actorId].filter(
        (value): value is string =>
          value !== null && value !== undefined && value.length > 0,
      );

      return {
        eventType: EVENT_LABELS[event.eventType] ?? event.eventType.replaceAll('_', ' '),
        occurredAt: event.occurredAt,
        actorText: actorParts.length > 0 ? actorParts.join(' / ') : 'Джерело не вказано',
      };
    }),
  );

}
