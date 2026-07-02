const SUPPORT_QR_PREFIX = 'SDV-SUPPORT:v1:';
const CORRELATION_ID_PATTERN = /^[A-Z2-7]{10}$/;

export function isSupportCorrelationId(correlationId: string): boolean {
  return CORRELATION_ID_PATTERN.test(correlationId);
}

export function formatSupportQrPayload(correlationId: string): string {
  if (!isSupportCorrelationId(correlationId)) {
    throw new Error('Support correlation id must be 10 uppercase base32 characters.');
  }

  return `${SUPPORT_QR_PREFIX}${correlationId}`;
}
