import { CustomerProfile } from '../../customer-profile/model/customer-profile.types';

export type RedemptionInspectStatus =
  | 'started'
  | 'profile_not_found'
  | 'sms_send_failed'
  | 'sms_sent'
  | 'sms_failed'
  | 'barcode_issued'
  | 'barcode_consumed'
  | 'barcode_expired'
  | 'unknown';

export type RedemptionInspectBarcodeStatus =
  | 'active'
  | 'expired'
  | 'consumed'
  | 'replaced_by_new_flow'
  | 'invalid_format'
  | 'not_issued'
  | 'unknown';

export interface RedemptionInspectRequest {
  readonly correlationId?: string;
  readonly barcodeValue?: string;
}

export interface RedemptionInspectRedemption {
  readonly status: RedemptionInspectStatus;
  readonly startedAt?: string | null;
  readonly lastEventAt?: string | null;
}

export interface RedemptionInspectBarcode {
  readonly value?: string | null;
  readonly formatValid: boolean;
  readonly phoneRuntimeKey?: string | null;
  readonly correlationId?: string | null;
  readonly status: RedemptionInspectBarcodeStatus;
  readonly expiresAt?: string | null;
  readonly consumedAt?: string | null;
  readonly consumedByScanId?: string | null;
}

export interface RedemptionInspectScan {
  readonly scanId?: string | null;
  readonly parsed?: Record<string, unknown> | null;
}

export interface RedemptionInspectAuditEvent {
  readonly eventType: string;
  readonly occurredAt: string;
  readonly actorType?: string | null;
  readonly actorId?: string | null;
  readonly metadata?: Record<string, unknown> | null;
}

export interface RedemptionInspectResponse {
  readonly correlationId: string;
  readonly redemption: RedemptionInspectRedemption;
  readonly barcode?: RedemptionInspectBarcode | null;
  readonly scan?: RedemptionInspectScan | null;
  readonly profile?: CustomerProfile | null;
  readonly auditEvents: readonly RedemptionInspectAuditEvent[];
}
