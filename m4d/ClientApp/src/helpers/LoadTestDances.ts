// @ts-ignore
import dancesJson from "@/assets/content/dances.json";
// @ts-ignore
import groupsJson from "@/assets/content/dancegroups.json";
// @ts-ignore
import metricsJson from "@/assets/content/metrics.json";

export function loadTestDances(): string {
  return JSON.stringify({ dances: dancesJson, groups: groupsJson, metrics: metricsJson });
}
