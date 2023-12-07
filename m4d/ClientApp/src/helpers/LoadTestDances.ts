// @ts-ignore
import dancesJson from "@/assets/dances.json";
// @ts-ignore
import groupsJson from "@/assets/dancegroups.json";

export function loadTestDances(): string {
  return JSON.stringify({ dances: dancesJson, groups: groupsJson });
}
