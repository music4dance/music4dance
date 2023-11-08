import { jsonMember, jsonObject } from "typedjson";
import { PurchaseEncoded } from "./Purchase";

@jsonObject
export class AlbumDetails {
  @jsonMember(Number) public index?: number;
  @jsonMember(String) public name?: string;
  @jsonMember(Number) public track?: number;
  @jsonMember(String) public publisher?: string;
  @jsonMember(PurchaseEncoded) public purchase!: PurchaseEncoded;

  public constructor(init?: Partial<AlbumDetails>) {
    Object.assign(this, init);
    if (!this.purchase) {
      this.purchase = new PurchaseEncoded();
    }
  }
}
