namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Giveaway Prize",
        description: "Gets the Giveaway Prize (use %prize%)",
        icon: MdiIcons.Gift,
        color: "Default",
        tableName: "subactions_giveawayprize")]
    public class GiveawayPrizeType : SimpleSubActionType
    {
        public GiveawayPrizeType()
        {
            SubActionTypes = SubActionTypes.GiveawayPrize;
        }

        protected override string TextLabel => "Prize Variable";
        protected override string TextHelperText => "The prize will be available as %prize% variable";
    }
}
