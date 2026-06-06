import { Song } from "@/models/Song";

export const displayTempoForSongTable = (
  song: Song,
  danceId?: string,
  tempoHidden: boolean = false,
): string => {
  if (tempoHidden) {
    return "";
  }

  const tempo = song.tempoForDance(danceId);
  return tempo ? `${Math.round(tempo)}` : "";
};
