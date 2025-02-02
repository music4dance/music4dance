import dancesJson from "@/assets/content/dances.json";
import groupsJson from "@/assets/content/dancegroups.json";
import metricsJson from "@/assets/content/metrics.json";

export function loadTestDances(): string {
  return JSON.stringify({ dances: dancesJson, groups: groupsJson, metrics: metricsJson });
}
