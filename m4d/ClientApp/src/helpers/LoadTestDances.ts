// @ts-ignore
import dancesJson from "@/assets/dances.json";
// @ts-ignore
import groupsJson from "@/assets/dancegroups.json";
// @ts-ignore
import metricsJson from "@/assets/metrics.json";

export function loadTestDances(): string {
  return JSON.stringify({ dances: dancesJson, groups: groupsJson, metrics: metricsJson });
}
