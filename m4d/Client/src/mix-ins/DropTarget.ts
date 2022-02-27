import { ServiceMatcher } from "@/model/ServiceMatcher";
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";

@Component
export default class DropTarget extends Vue {
  private serviceMatcher: ServiceMatcher = new ServiceMatcher();

  protected async checkServiceAndWarn(input: string): Promise<void> {
    const matcher = this.serviceMatcher;
    const service = matcher.match(input);
    if (!service) {
      return;
    }
    const found = await this.checkService(input);

    if (!found) {
      const okay = await this.$bvModal.msgBoxConfirm(
        `It looks like you may have tried to search by ${service.name} id for a song not in the music4dance catalog.
         Would you like to add the song?`,
        {
          title: "Music Service Search?",
          okTitle: "Add Song",
        }
      );
      if (okay) {
        const id = matcher.parseId(input, service);
        window.location.href = `/song/augment?id=${id}`;
      }
    }
  }

  protected async checkService(input: string): Promise<boolean> {
    const matcher = this.serviceMatcher;
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
}
