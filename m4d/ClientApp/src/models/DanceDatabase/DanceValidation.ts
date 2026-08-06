import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

// Mirrors DanceLib/DanceValidation.cs - sanity-check thresholds used to catch Spotify/EchoNest
// half-time/double-time tempo detection errors. Lives on DanceType (dances.json's top-level
// "validation" key), not per style/instance - see DanceType.Validation in DanceLib for why.
@jsonObject
export class DanceValidation {
  @jsonMember(Number) public doubleTempoIfBelow?: number;
  @jsonMember(Number) public halveTempoIfAbove?: number;
  @jsonArrayMember(String) public flagInvalidMeters?: string[];
}
