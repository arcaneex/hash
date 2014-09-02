namespace RohBot.Commands
{
    public class Wiki : Command
    {
        public override string Type { get { return "wiki"; } }

        public override string Format(CommandTarget target, string type) { return "]"; }

        public override void Handle(CommandTarget target, string type, string[] parameters)
        {
            if ( !target.IsRoom || parameters.Length == 0)
                return;

            var username = target.Connection.Session.Account.Name;
            var room = target.Room;
            if (room.IsBanned(username))
            {
                target.Send("You are banned from this room.");
                return;
            }
            
            var line = new StateLine
            {
                Date = Util.GetCurrentTimestamp(),
                Chat = target.Room.RoomInfo.ShortName,
                State = "Action",
                For = "",
                ForId = ",
                ForType = "RohBot"
            };

            if (target.IsWeb)
            {
                var byAccount = target.Connection.Session.Account;
                line.By = byAccount.Name;
                line.ById = byAccount.Id.ToString("D");
                line.ByType = "RohBot";
            }
            else
            {
                line.By = target.Persona.DisplayName;
                line.ById = target.Persona.Id.ConvertToUInt64().ToString("D");
                line.ByType = "Steam";
            }

            line.Content = string.Format("http://glua.me/docs/#?f={0}", parameters[0]);
               
            target.Room.SendLine(line);
            return;
        }
    }
}
