import { setPacusStage } from "./character.js";

export function applyGrowth(character, pacus) {
  const stage = pacus?.stage ?? "baby";
  setPacusStage(character, stage);
  character.userData.growth = {
    stage,
    size: Number(pacus?.size ?? 0),
    totalClosedDays: Number(pacus?.totalClosedDays ?? 0),
  };
  return character.userData.growth;
}

export function getGrowthLabel(stage) {
  const labels = {
    egg: "Ovo",
    cracking: "Rachando",
    hatching: "Nascendo",
    baby: "Filhote",
    young: "Jovem",
    adult: "Adulto",
  };
  return labels[String(stage).toLowerCase()] ?? "Filhote";
}
