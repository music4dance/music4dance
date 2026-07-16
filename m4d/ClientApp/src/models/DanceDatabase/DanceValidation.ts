import { jsonMember, jsonObject } from "typedjson";

// Mirrors DanceLib/DanceValidation.cs - sanity-check thresholds used to catch Spotify/EchoNest
// half-time/double-time tempo detection errors. Currently only populated for Salsa's "Social"
// instance in dances.json.
@jsonObject
export class DanceValidation {
  @jsonMember(Number) public doubleTempoIfBelow?: number;
  @jsonMember(Number) public halveTempoIfAbove?: number;
}
