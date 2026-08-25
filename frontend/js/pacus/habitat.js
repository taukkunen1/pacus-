// Renderiza o tanque (elemento de assinatura). Por ora um estado visual fixo —
// variacao por estagio (ovo/rachando/... adulto) fica para pacus/growth.js.
export function renderTank() {
  return `
    <div class="pacus-tank" aria-hidden="true">
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="tank-bubble"></div>
      <div class="pacus-body">
        <div class="pacus-body__gill"></div>
        <div class="pacus-body__gill"></div>
        <div class="pacus-body__gill"></div>
        <div class="pacus-body__torso"></div>
      </div>
      <div class="tank-rock tank-rock--1"></div>
      <div class="tank-rock tank-rock--2"></div>
      <div class="tank-floor"></div>
      <span class="tank-caption">Pacus esta por aqui em algum lugar 👀</span>
    </div>
  `;
}
