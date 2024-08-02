import { jsonMember, jsonObject } from "typedjson";

@jsonObject
export class DanceMetrics {
  @jsonMember(String) public id!: string;
  @jsonMember(Number) public songCount!: number;
  @jsonMember(Number) public maxWeight!: number;
}
