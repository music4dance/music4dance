import { DanceEnvironment } from "@/model/DanceEnvironment";
import { TypedJSON } from "typedjson";
import environmentJson from "../assets/dance-environment.json";

declare global {
  interface Window {
    environment?: DanceEnvironment;
  }
}

export function getEnvironmentMock(): DanceEnvironment {
  if (!window.environment) {
    window.environment = TypedJSON.parse(environmentJson, DanceEnvironment);
  }

  return window.environment!;
}
