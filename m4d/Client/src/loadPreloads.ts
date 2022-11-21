import {
  getEnvironment,
  getTagDatabase,
} from "./helpers/DanceEnvironmentManager";
import { getEnvironmentMock } from "./helpers/MockEnvironmentManager";
import { getTagDatabaseMock } from "./helpers/MockTagDatabaseManager";
import { Preloads } from "./Preloads";

export async function loadPreloads(preloads: Preloads): Promise<void> {
  if (Preloads.Dances === (preloads & Preloads.Dances)) {
    getEnvironmentMock();
    await getEnvironment();
  }
  if (Preloads.Tags === (preloads & Preloads.Tags)) {
    getTagDatabaseMock();
    await getTagDatabase();
  }
}
