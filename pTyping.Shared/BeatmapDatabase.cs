using pTyping.Shared.Beatmaps;
using pTyping.Shared.Beatmaps.HitObjects;
using pTyping.Shared.Events;
using Realms;
using Realms.Schema;

namespace pTyping.Shared;

public class BeatmapDatabase {
	public readonly Realm Realm;

	public const ulong SCHEMA_VERSION = 1;

	public BeatmapDatabase() {
		RealmSchema.Builder builder = new RealmSchema.Builder {
			typeof(BeatmapSet),
			typeof(Beatmap),
			typeof(Break),
			typeof(BeatmapDifficulty),
			typeof(BeatmapFileCollection),
			typeof(BeatmapInfo),
			typeof(BeatmapMetadata),
			typeof(PathHashTuple),
			typeof(TimingPoint),
			typeof(HitObject),
			typeof(Event),
			typeof(AsciiUnicodeTuple),
			typeof(HitObjectColor),
			typeof(HitObjectSettings)
		};

		RealmConfiguration config = new RealmConfiguration(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "songs.db")) {
			Schema            = builder.Build(),
			SchemaVersion     = SCHEMA_VERSION,
			MigrationCallback = this.Migrate
		};

		this.Realm = Realm.GetInstance(config);
	}
	private void Migrate(Migration migration, ulong oldSchemaVersion) {
		List<dynamic>          oldSets = migration.OldRealm.DynamicApi.All("BeatmapSet").ToList();
		IQueryable<BeatmapSet> newSets = migration.NewRealm.All<BeatmapSet>();

		for (int i = 0; i < newSets.Count(); i++) {
			dynamic    oldSet = oldSets.ElementAt(i);
			BeatmapSet newSet = newSets.ElementAt(i);

			//If the old version is less than 1, than migrate to proper backlinks
			if (oldSchemaVersion < 1) {
				//This is blank because nothing needs to be done here, this is just here to keep track of the change
			}
		}
	}
}
