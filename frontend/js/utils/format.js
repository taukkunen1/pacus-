const BRL_FORMATTER = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

export function formatBrl(value) {
  return BRL_FORMATTER.format(value);
}

const PERIOD_LABELS = { morning: "Manha", afternoon: "Tarde", evening: "Noite" };
export function periodLabel(period) {
  return PERIOD_LABELS[period] ?? period;
}

const TYPE_LABELS = { mandatory: "Obrigatorias", expected: "Deve fazer", challenge: "Desafios" };
export function typeLabel(type) {
  return TYPE_LABELS[type] ?? type;
}
