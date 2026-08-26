export const POINTS_MIN = -10;
export const POINTS_MAX = 10;

export const POINTS_HELP_TEXT =
  `Pacus Points (de ${POINTS_MIN} a ${POINTS_MAX}, sem zero \u2014 negativo conta como penalidade)`;

export function isValidPoints(points) {
  return (
    Number.isInteger(points) &&
    points !== 0 &&
    points >= POINTS_MIN &&
    points <= POINTS_MAX
  );
}
