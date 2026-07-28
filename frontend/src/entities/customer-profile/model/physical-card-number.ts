const PHYSICAL_CARD_NUMBER_LENGTH = 13;

export function isValidPhysicalCardNumber(value: string): boolean {
  if (!/^\d{13}$/.test(value)) {
    return false;
  }

  let checksum = 0;

  for (let index = 0; index < PHYSICAL_CARD_NUMBER_LENGTH - 1; index += 1) {
    checksum += Number(value[index]) * (index % 2 === 0 ? 1 : 3);
  }

  return (10 - (checksum % 10)) % 10 === Number(value.at(-1));
}
