import {
  getEnvironment,
  getTagDatabase,
} from "./helpers/DanceEnvironmentManager";
import { Preloads } from "./Preloads";

// TODONEXT:
//  Allow adding a dance with zero songs (maybe just for diag?)
export async function loadPreloads(preloads: Preloads): Promise<void> {
  if (Preloads.Dances === (preloads & Preloads.Dances)) {
    await getEnvironment();
  }
  if (Preloads.Tags === (preloads & Preloads.Tags)) {
    await getTagDatabase();
  }
}
