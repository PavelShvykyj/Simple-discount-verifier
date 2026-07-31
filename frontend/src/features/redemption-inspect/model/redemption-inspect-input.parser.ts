import { RedemptionInspectRequest } from '../../../entities/redemption-inspect/model/redemption-inspect.types';

const SUPPORT_QR_PREFIX = 'SDV-SUPPORT:V1:';
const CORRELATION_ID_PATTERN = /^[A-Z2-7]{10}$/;
const BARCODE_VALUE_PATTERN = /^[A-Z2-7]{20}$/;

export type RedemptionInspectInputKind = 'correlationId' | 'barcodeValue' | 'supportQr';

export interface ParsedRedemptionInspectInput {
  readonly kind: RedemptionInspectInputKind;
  readonly request: RedemptionInspectRequest;
}

export function parseRedemptionInspectInput(
  value: string,
): ParsedRedemptionInspectInput | null {
  const normalizedValue = value.trim().toUpperCase();

  if (normalizedValue.startsWith(SUPPORT_QR_PREFIX)) {
    const correlationId = normalizedValue.slice(SUPPORT_QR_PREFIX.length);

    return CORRELATION_ID_PATTERN.test(correlationId)
      ? {
          kind: 'supportQr',
          request: { correlationId },
        }
      : null;
  }

  if (CORRELATION_ID_PATTERN.test(normalizedValue)) {
    return {
      kind: 'correlationId',
      request: { correlationId: normalizedValue },
    };
  }

  if (BARCODE_VALUE_PATTERN.test(normalizedValue)) {
    return {
      kind: 'barcodeValue',
      request: { barcodeValue: normalizedValue },
    };
  }

  return null;
}
