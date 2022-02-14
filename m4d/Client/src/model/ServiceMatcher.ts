import axios from "axios";
import { TypedJSON } from "typedjson";
import { SongDetailsModel } from "./SongDetailsModel";

export interface Service {
  id: string;
  name: string;
  rgx: RegExp[];
}

const services: Service[] = [
  {
    id: "i",
    name: "Apple Music",
    rgx: [
      /(^\d{7,10})$/gi,
      /https:\/\/(?:music|itunes)\.apple\.com.*\/\d{7,10}\?i=(\d{7,10})/gi,
    ],
  },
  {
    id: "s",
    name: "Spotify",
    rgx: [
      /^([a-z0-9]{22})$/gi,
      /https:\/\/open\.spotify\.com\/track\/([a-z0-9]{22})/gi,
    ],
  },
];

//  Making this an instanciable class because eventually
//   we'll want the contructor to specificy what services to handle
export class ServiceMatcher {
  public match(serviceString: string): Service | undefined {
    return services.find((s) => this.matchService(serviceString, s));
  }

  private matchService(id: string, service: Service): boolean {
    return service.rgx.some((rgx) => id.match(rgx));
  }

  public parseId(id: string, service: Service): string {
    const rgx = service.rgx.find((r) => id.match(r));
    if (!rgx) {
      throw new Error(`Invalid id ${id}: No regex found for ${service.name}`);
    }
    const match = rgx.exec(id);
    if (!match) {
      throw new Error(`Invalid id ${id}: No match found for ${service.name}`);
    }

    return match[1];
  }

  public async findSong(
    serviceString: string,
    localOnly = false
  ): Promise<SongDetailsModel | undefined> {
    const service = this.match(serviceString);
    if (service) {
      try {
        const id = this.parseId(serviceString, service);
        const uri = `/api/servicetrack/${service.id}${id}?localOnly=${localOnly}`;
        const response = await axios.get(uri);
        const songModel = TypedJSON.parse(response.data, SongDetailsModel);
        console.log("Song found");
        return songModel;
      } catch (e) {
        // Swallow errors
      }
    }
    console.log("Song not found");
    return undefined;
  }
}
