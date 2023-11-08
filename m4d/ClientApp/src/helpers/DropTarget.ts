import { ServiceMatcher } from "./ServiceMatcher";

const matcher: ServiceMatcher = new ServiceMatcher();

export async function checkServiceAndAdd(input: string, warn?: boolean): Promise<void> {
  const service = matcher.match(input);
  if (!service) {
    return;
  }
  const found = await checkService(input);

  if (!found) {
    let okay = false;
    if (warn) {
      // INT-TODO: Figure out if there is a way to do this with BSV-Next
      //   okay = await this.$bvModal.msgBoxConfirm(
      //     `It looks like you may have tried to search by ${service.name} id for a song not in the music4dance catalog.
      //  Would you like to add the song?`,
      //     {
      //       title: "Music Service Search?",
      //       okTitle: "Add Song",
      //     },
      //   );

      okay = window.confirm(
        `It looks like you may have tried to search by ${service.name} id for a song not in the music4dance catalog. Would you like to add the song?`,
      );
    }
    if (okay) {
      const id = matcher.parseId(input, service);
      window.location.href = `/song/augment?id=${id}`;
    }
  }
}

export async function checkServiceAndWarn(input: string): Promise<void> {
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
