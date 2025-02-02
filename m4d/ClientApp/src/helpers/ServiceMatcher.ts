import axios from "axios";
import { TypedJSON } from "typedjson";
import { PlaylistModel } from "@/models/PlaylistModel";
import { ServiceUser } from "@/models/ServiceUser";
import { SongDetailsModel } from "@/models/SongDetailsModel";

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
      /\s*['"]?(\d{7,10})['"]?\s*$/gi,
      /\s*['"]https:\/\/(?:music|itunes)\.apple\.com.*\/\d{7,10}\?i=(\d{7,10})['"]?\s*$/gi,
    ],
  },
  {
    id: "s",
    name: "Spotify",
    rgx: [
      /\s*['"]?([a-z0-9]{22})['"]?\s*$/gi,
      /\s*['"]https:\/\/open\.spotify\.com\/track\/([a-z0-9]{22})['"]?\s*$/gi,
    ],
  },
];

//  Making this an instanciable class because eventually
//   we'll want the contructor to specificy what services to handle
export class ServiceMatcher {
  public match(serviceString: string): Service | undefined {
    return serviceString ? services.find((s) => this.matchService(serviceString, s)) : undefined;
  }

  private matchService(id: string, service: Service): boolean {
    return id ? service.rgx.some((rgx) => id.match(rgx)) : false;
  }

  public parseId(id: string, service: Service): string {
    const rgx = service.rgx.find((r) => id.match(r));
    if (!rgx) {
      throw new Error(`Invalid id ${id}: No regex found for ${service.name}`);
    }
    const ret = this.parse([rgx], id);

    if (!ret) {
      throw new Error(`Invalid id ${id}: No match found for ${service.name}`);
    }

    return ret;
  }

  public parsePlaylist(id: string): string | null {
    const patterns = [/https:\/\/open\.spotify\.com\/playlist\/([a-z0-9]{22})/gi];
    return this.parse(patterns, id);
  }

  public parseUser(id: string): string | null {
    const patterns = [/https:\/\/open\.spotify\.com\/user\/([^?/]*)/gi];
    return this.parse(patterns, id);
  }

  private parse(patterns: RegExp[], id: string): string | null {
    const rgx = patterns.find((r) => id.match(r));
    if (!rgx) {
      return null;
    }
    const match = rgx.exec(id);
    if (!match) {
      return null;
    }
    return match[1];
  }

  public async findSong(
    serviceString: string,
    localOnly = false,
  ): Promise<SongDetailsModel | undefined> {
    const service = this.match(serviceString);
    if (service) {
      try {
        const id = this.parseId(serviceString, service);
        const uri = `/api/servicetrack/${service.id}${id}?localOnly=${localOnly}`;
        const response = await axios.get(uri);
        const songModel = TypedJSON.parse(response.data, SongDetailsModel);
        return songModel;
      } catch {
        // Swallow errors
      }
    }
    return undefined;
  }

  public async findSpotifyPlaylist(serviceString: string): Promise<PlaylistModel | undefined> {
    const id = this.parsePlaylist(serviceString);
    if (id) {
      try {
        const uri = `/api/serviceplaylist/s${id}`;
        const response = await axios.get(uri);
        const trackModel = TypedJSON.parse(response.data, PlaylistModel);
        return trackModel;
      } catch {
        // Swallow errors
      }
    }
    return undefined;
  }

  public async findSpotifyUser(serviceString: string): Promise<ServiceUser | undefined> {
    const id = this.parseUser(serviceString);
    if (id) {
      try {
        const uri = `/api/serviceuser/s${id}`;
        const response = await axios.get(uri);
        const userModel = TypedJSON.parse(response.data, ServiceUser);
        return userModel;
      } catch {
        // Swallow errors
      }
    }
    return undefined;
  }
}
