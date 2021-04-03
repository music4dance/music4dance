import "reflect-metadata";
import { getEnvironmentMock } from "./helpers/MockEnvironmentManager";

/* eslint-disable-next-line @typescript-eslint/no-explicit-any */
declare const global: any;

beforeAll(() => {
  global.environment = getEnvironmentMock();
});
