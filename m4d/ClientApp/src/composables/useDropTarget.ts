import { ServiceMatcher } from "../helpers/ServiceMatcher";
import { useModalController } from "bootstrap-vue-next";

const matcher: ServiceMatcher = new ServiceMatcher();

export function useDropTarget() {
  const { confirm } = useModalController();

  async function checkServiceAndAdd(input: string, warn?: boolean): Promise<void> {
    const service = matcher.match(input);
    if (!service) {
      return;
    }
    const found = await checkService(input);

    if (!found) {
      let okay = false;
      if (warn) {
        okay =
          (await confirm?.({
            props: {
              body: `It looks like you may have tried to search by ${service.name} id for a song not in the music4dance catalog. Would you like to add the song?`,
              title: "Song not found",
              okTitle: "Yes",
              cancelTitle: "No",
            },
          })) ?? false;
      }
      if (okay) {
        const id = matcher.parseId(input, service);
        window.location.href = `/song/augment?id=${id}`;
      }
    }
  }

  async function checkServiceAndWarn(input: string): Promise<void> {
    return checkServiceAndAdd(input, true);
  }

  async function checkService(input: string): Promise<boolean> {
    const service = matcher.match(input);
    if (service) {
      try {
        const id = matcher.parseId(input, service);
        if (id) {
          const song = await matcher.findSong(input, true);
          if (song) {
            window.location.href = `/song/details/${song.songHistory.id}`;
            return true;
          }
        }
      } catch {
        // swallow any errors
      }
    }
    return false;
  }

  return { checkServiceAndAdd, checkServiceAndWarn };
}
