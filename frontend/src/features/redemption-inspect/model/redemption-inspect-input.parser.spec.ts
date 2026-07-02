import { parseRedemptionInspectInput } from './redemption-inspect-input.parser';

describe('parseRedemptionInspectInput', () => {
  it('parses support QR payloads as correlation id lookups', () => {
    expect(parseRedemptionInspectInput('SDV-SUPPORT:V1:qps7o7kcnm')).toEqual({
      kind: 'supportQr',
      request: { correlationId: 'QPS7O7KCNM' },
    });
  });

  it('parses plain correlation ids and barcode values', () => {
    expect(parseRedemptionInspectInput(' qps7o7kcnm ')).toEqual({
      kind: 'correlationId',
      request: { correlationId: 'QPS7O7KCNM' },
    });
    expect(parseRedemptionInspectInput('eobmcdrdrtqps7o7kcnm')).toEqual({
      kind: 'barcodeValue',
      request: { barcodeValue: 'EOBMCDRDRTQPS7O7KCNM' },
    });
  });

  it('rejects unsupported or malformed values', () => {
    expect(parseRedemptionInspectInput('SDV-SUPPORT:V1:bad')).toBeNull();
    expect(parseRedemptionInspectInput('not a support code')).toBeNull();
    expect(parseRedemptionInspectInput('1234567890')).toBeNull();
  });
});
