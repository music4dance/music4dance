import { ref } from "vue";

export function useSongSelector() {
  const songs = ref<string[]>([]);
  const select = (id: string, selected: boolean) => {
    if (selected) {
      if (!songs.value.find((s) => s === id)) {
        songs.value.push(id);
      }
    } else {
      songs.value = songs.value.filter((s) => s !== id);
    }
  };
  return { songs, select };
}
