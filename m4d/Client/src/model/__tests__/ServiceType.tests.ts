import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { PurchaseEncoded, ServiceType, PurchaseInfo } from "../Purchase";

function getEncoded(): PurchaseEncoded {
  const encoded = {
    sa: "08nZoEXyWuk8PFLkUUBHY8",
    ss: "0lhmmb5WRNwzz0mAZRMNgK[AD,AE,AR,AT,AU,BE,BG,BH,BO,BR,CA,CH,CL,CO,CR]",
    as: "D:B01BXHIVUY",
    aa: "D:B01BXHIQU4",
    is: "1086781400",
    ia: "1086781393",
  };

  return TypedJSON.parse(encoded, PurchaseEncoded)!;
}

describe("service type", () => {
  it("should load encoded", () => {
    const encoded = getEncoded();
    expect(encoded).toBeDefined();
    expect(encoded).toBeInstanceOf(PurchaseEncoded);
  });

  it("should decode amazon", () => {
    const amazon = getEncoded().decodeService(ServiceType.Amazon);

    expect(amazon).toBeDefined();
    expect(amazon).toBeInstanceOf(PurchaseInfo);
    expect(amazon?.albumId).toEqual("D:B01BXHIQU4");
    expect(amazon?.songId).toEqual("D:B01BXHIVUY");
  });

  it("should decode itunes", () => {
    const itunes = getEncoded().decodeService(ServiceType.ITunes);

    expect(itunes).toBeDefined();
    expect(itunes).toBeInstanceOf(PurchaseInfo);
    expect(itunes?.albumId).toEqual("1086781393");
    expect(itunes?.songId).toEqual("1086781400");
  });

  it("should decode spotify", () => {
    const spotify = getEncoded().decodeService(ServiceType.Spotify);

    expect(spotify).toBeDefined();
    expect(spotify).toBeInstanceOf(PurchaseInfo);
    expect(spotify?.albumId).toEqual("08nZoEXyWuk8PFLkUUBHY8");
    expect(spotify?.songId).toEqual("0lhmmb5WRNwzz0mAZRMNgK");
  });

  it("should decode purchaseinfo", () => {
    const pi = getEncoded().decode();

    expect(pi).toBeDefined();
    expect(pi).toBeInstanceOf(Array);
    expect(pi.length).toEqual(3);
    expect(pi[0]).toBeInstanceOf(PurchaseInfo);
  });
});
