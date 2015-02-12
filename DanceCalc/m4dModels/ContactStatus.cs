using System;

namespace m4dModels
{
    [Flags]
    public enum ContactStatus : byte
    {
        None,
        DirectPromotion = 0x01,
        PartnerPromotion = 0x02,
        SurveyFriendly = 0x04,
        Max = 0x08
    };
}