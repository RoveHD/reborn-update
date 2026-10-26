using PokemonRebornLauncher.Services;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
var session = new HalloweenSecretSession();
var book = session.Activate(PropKind.Spellbook);
Check(book?.Name == "Die vergessene Formel" && book.Hint == "Kerzenlicht, Trank, Kessel", "Readable book clue");
Check(session.Activate(PropKind.Spellbook) is null, "Book no farming");
Check(session.Activate(PropKind.Bat)?.Name == "Wächter der Nacht", "Bat discovery");
Check(session.Activate(PropKind.Bat) is null, "Bat no farming");
session.Activate(PropKind.Potion); Check(session.Activate(PropKind.Cauldron) is null, "Potion alone does not solve recipe");
session.Activate(PropKind.Candle); session.Activate(PropKind.Cauldron); session.Activate(PropKind.Potion);
Check(session.Activate(PropKind.Cauldron) is null, "Wrong order rejected");
session.Activate(PropKind.Candle); session.Activate(PropKind.Potion); session.Activate(PropKind.Pumpkin);
Check(session.Activate(PropKind.Cauldron) is null, "Unrelated click interrupts sequence");
session.Activate(PropKind.Candle); session.Activate(PropKind.Potion); session.Interrupt();
Check(session.Activate(PropKind.Cauldron) is null, "Theme/disable interruption clears unfinished sequence");
session.Activate(PropKind.Candle); session.Activate(PropKind.Potion);
Check(session.Activate(PropKind.Cauldron)?.Name == "Nachtelixier", "Correct recipe discovered");
session.Activate(PropKind.Candle); session.Activate(PropKind.Potion);
Check(session.Activate(PropKind.Cauldron) is null && session.Discovered.Count == 3, "Recipe no farming");
session.Interrupt(); Check(session.Discovered.Count == 3, "Confirmed discoveries survive theme switch");

var simulation = new EventPropSimulation(17); simulation.Reset("halloween");
Check(simulation.Props.Count(p => p.Kind == PropKind.Spellbook) == 1 && simulation.Props.Count(p => p.Kind == PropKind.Potion) == 1, "New props in catalog");
foreach (var kind in new[] { PropKind.Bat, PropKind.Spellbook, PropKind.Potion })
{
    var prop = simulation.Props.Single(p => p.Kind == kind); double x = prop.X, y = prop.Y;
    Check(simulation.Activate(prop.Id), kind + " activates");
    for (int i = 0; i < (kind == PropKind.Bat ? 400 : 40); i++) simulation.Step(.05);
    Check(prop.State == PropState.Idle && Math.Abs(prop.X-x) < .001 && Math.Abs(prop.Y-y) < .001, kind + " returns to authored home");
}
var cauldron = simulation.Props.Single(p => p.Kind == PropKind.Cauldron);
simulation.Configure(true, 6, 1); simulation.CelebrateHalloweenSecret(cauldron.Id, false); simulation.Step(.05);
Check(simulation.Particles.Count <= 6 && simulation.Props.Count(p => p.Kind == PropKind.Ghost && p.IsTemporary) == 1, "Celebration respects budget and creates one painted ghost");
for (int i = 0; i < 40; i++) simulation.Step(.05);
Check(!simulation.Props.Any(p => p.Kind == PropKind.Ghost && p.IsTemporary), "Secret ghost expires");
foreach (bool reduced in new[] { false, true })
{
    simulation.Reset("halloween"); simulation.Configure(reduced, 6, 1);
    simulation.CelebrateHalloweenSecret(simulation.Props.First(p => p.Kind == PropKind.Cauldron).Id, reduced); simulation.Step(.05);
    Check(simulation.Particles.Count == 0 && simulation.Props.All(p => !p.IsTemporary), "Disabled or reduced celebration emits no effects");
}
Console.WriteLine($"HALLOWEEN_SECRET_OK {checks} checks");
