import { TypedJSON } from "typedjson";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import environmentJson from "../assets/dance-environment.json";

declare global {
  interface Window {
    environment?: DanceEnvironment;
  }
}

export function getEnvironmentMock(): DanceEnvironment | undefined {
  if (!window.environment) {
    window.environment = TypedJSON.parse(environmentJson, DanceEnvironment);
  }

  return window.environment;
}
