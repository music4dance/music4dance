using System;
namespace m4dModels;

[Flags]
public enum CruftFilter
{
    NoCruft = 0x00,
    NoPublishers = 0x01,
    NoDances = 0x02,
    AllCruft = 0x03
}
