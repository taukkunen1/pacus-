const WEEKDAY_MONTH_FORMATTER = new Intl.DateTimeFormat("pt-BR", {
  weekday: "long",
  day: "2-digit",
  month: "long",
});

// Espera uma data no formato YYYY-MM-DD (a "data operacional" que a API usa).
export function formatOperationalDate(isoDate) {
  const [year, month, day] = isoDate.split("-").map(Number);
  const date = new Date(year, month - 1, day);
  return WEEKDAY_MONTH_FORMATTER.format(date);
}
