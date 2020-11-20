import axios from "axios";
import { TypedJSON } from "typedjson";
import { DanceEnvironment } from "@/model/DanceEnvironmet";

declare global {
  interface Window {
    environment?: DanceEnvironment;
  }
}

export async function getEnvironment(): Promise<DanceEnvironment> {
  if (window.environment) {
    return window.environment;
  }

  window.environment = loadFromStorage();
  if (window.environment) {
    return window.environment;
  }

  return loadStats();
}

async function loadStats(): Promise<DanceEnvironment> {
  try {
    const response = await axios.get(`/api/dancesstatistics/`);
    const data = response.data;
    sessionStorage.setItem("dance-stats", JSON.stringify(data));
    window.environment = TypedJSON.parse(data, DanceEnvironment);
    return window.environment!;
  } catch (e) {
    console.log(e);
    throw e;
  }
}

function loadFromStorage(): DanceEnvironment | undefined {
  const statString = sessionStorage.getItem("dance-stats");

  if (!statString) {
    return;
  }

  window.environment = TypedJSON.parse(statString, DanceEnvironment);
  return window.environment;
}
