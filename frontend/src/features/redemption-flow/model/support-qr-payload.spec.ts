import { formatSupportQrPayload, isSupportCorrelationId } from './support-qr-payload';

describe('support QR payload', () => {
  it('formats the neutral support payload for a valid correlation id', () => {
    expect(formatSupportQrPayload('QPS7O7KCNM')).toBe('SDV-SUPPORT:v1:QPS7O7KCNM');
  });

  it('accepts only 10 uppercase base32 characters as support correlation id', () => {
    expect(isSupportCorrelationId('QPS7O7KCNM')).toBe(true);
    expect(isSupportCorrelationId('qps7o7kcnm')).toBe(false);
    expect(isSupportCorrelationId('QPS7O7KCN1')).toBe(false);
    expect(isSupportCorrelationId('QPS7O7KCNM2')).toBe(false);
  });

  it('throws when formatting an invalid support correlation id', () => {
    expect(() => formatSupportQrPayload('invalid-id')).toThrow(
      'Support correlation id must be 10 uppercase base32 characters.',
    );
  });
});
