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

            if (parameters[0].Contains('.')) {
                target.Send("http://glua.me/docs/#?f="+parameters[0]);
                return;
            }
        }
    }
}
