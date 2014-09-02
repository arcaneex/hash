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

            var send = string.Format("http://glua.me/docs/#?f={0}", parameters[0]);
            target.Room.SendLine(line);
            return;
        }
    }
}
