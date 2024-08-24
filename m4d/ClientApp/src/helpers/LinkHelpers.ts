import type { DanceInstance } from "@/models/DanceDatabase/DanceInstance";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import type { TempoRange } from "@/models/DanceDatabase/TempoRange";
import { wordsToKebab } from "./StringHelpers";

export function danceLink(dance: DanceInstance): string {
  return wordsToKebab(dance.shortName);
}

export function tempoLink(dance: DanceInstance | DanceType, tempo: TempoRange): string {
  return `/song/advancedsearch?dances=${dance.baseId}&tempomin=${tempo.min}&tempomax=${tempo.max}&sortorder=Dances`;
}

export function defaultTempoLink(dance: DanceInstance | DanceType): string {
  return tempoLink(dance, dance.tempoRange);
}

export function filteredTempoLink(dance: DanceInstance, filter: string): string {
  return tempoLink(dance, dance.filteredTempo([filter])!);
}
