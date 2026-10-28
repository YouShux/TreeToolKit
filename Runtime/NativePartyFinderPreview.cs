using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using OmenTools.Interop.Game.Models;

namespace TreeToolKit;

public sealed unsafe class NativePartyFinderPreview
{
    private static readonly CompSig OpenListingDataSig = new(
        "48 89 5C 24 08 57 48 83 EC 20 48 8B DA 48 8B F9 E8 ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 48 8B D3 48 8B CF 48 8B 5C 24 30 48 83 C4 20 5F E9 ?? ?? ?? ??");
    private delegate void OpenListingDataDelegate(AgentLookingForGroup* agent, AgentLookingForGroup.Detailed* listing);
    private readonly OpenListingDataDelegate OpenListingData = OpenListingDataSig.GetDelegate<OpenListingDataDelegate>();

    public void Open(AgentLookingForGroup* agent, ref AgentLookingForGroup.Detailed listing, ushort dutyType)
    {
        if (agent == null || listing.ListingId == 0)
            throw new ArgumentException("A party finder agent and a listing are required.");

        fixed (AgentLookingForGroup.Detailed* data = &listing)
        {
            var fields = (DetailFields*)data;
            fields->DutyType = dutyType;
            fields->Result = 1;
            OpenListingData(agent, data);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x5D)]
    private struct DetailFields
    {
        [FieldOffset(0x2A)] public ushort DutyType;
        [FieldOffset(0x5C)] public byte Result;
    }
}
