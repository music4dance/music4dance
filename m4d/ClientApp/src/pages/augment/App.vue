<script setup lang="ts">
import { AugmentModel } from "@/models/AugmentModel";
import { Song } from "@/models/Song";
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { TypedJSON } from "typedjson";
import { getMenuContext } from "@/helpers/GetMenuContext";

import { computed, onMounted, ref } from "vue";

enum AugmentPhase {
  lookup = "lookup",
  results = "results",
  edit = "edit",
}

declare const model_: AugmentModel;

const context = getMenuContext();

const model = TypedJSON.parse(model_, AugmentModel) ?? {};
const phase = ref<AugmentPhase>(AugmentPhase.lookup);
const songModel = ref<SongDetailsModel | null>(null);
const lastSong = ref<Song | null>(null);
const created = ref(false);
const propertiesString = ref("");
const tabIndex = ref(0);

const canAugment = computed(() => !!context.userName);
const computedId = computed(() => (model.id ? model.id : undefined));

onMounted(() => {
  tabIndex.value = model.id ? 1 : 0;
});

const editSong = (model: SongDetailsModel): void => {
  songModel.value = new SongDetailsModel({
    created: !!model.created,
    songHistory: model.songHistory,
    filter: new SongFilter(),
    userName: context.userName,
  });
  phase.value = AugmentPhase.edit;
};

const adminCreate = async (): Promise<void> => {
  songModel.value = new SongDetailsModel({
    created: true,
    songHistory: SongHistory.fromString(propertiesString.value),
    filter: new SongFilter(),
    userName: context.userName,
  });
  propertiesString.value = "";
  phase.value = AugmentPhase.edit;
};

const reset = (saved: boolean): void => {
  if (saved) {
    lastSong.value = Song.fromHistory((songModel.value! as SongDetailsModel).songHistory);
    created.value = !!songModel.value!.created;
  }
  phase.value = AugmentPhase.lookup;
  model.id = undefined;
  songModel.value = null;
};
</script>

<template>
  <PageFrame id="app">
    <BRow v-if="phase === 'lookup'">
      <BCol>
        <BAlert v-if="lastSong" dismissible :model-value="1000">
          Thank you for {{ created ? "adding" : "editing" }} <i>{{ lastSong.title }}</i> by
          {{ lastSong.artist }}
        </BAlert>
        <h1>Add Song</h1>
        <BTabs v-if="canAugment" v-model="tabIndex" card>
          <BTab title="by Title"
            ><BCardText><AugmentSearch @edit-song="editSong($event)" /></BCardText
          ></BTab>
          <BTab title="by Id">
            <BCardText><AugmentLookup :id="computedId" @edit-song="editSong($event)" /></BCardText>
          </BTab>
          <BTab v-if="context.isAdmin" title="Admin">
            <BCardText>
              <p>Paste in text version of property list</p>
              <BInputGroup prepend="Properties">
                <BFormInput
                  id="admin-properties"
                  v-model="propertiesString"
                  palceholder="Song Properties TSV"
                  trim
                />
                <BButton variant="primary" @click="adminCreate">Create</BButton>
              </BInputGroup>
            </BCardText>
          </BTab>
        </BTabs>
        <AugmentInfo v-else :id="computedId" />
      </BCol>
    </BRow>
    <div v-else>
      <BAlert :model-value="true" variant="success">
        <div v-if="songModel && songModel.created">
          <b>Create Song:</b> This song is new to music4dance, please fill in missing fields and
          click <b>Add Song</b> to add this song to the catalog. Remember, you must vote on at least
          one dance to add this song.
        </div>
        <div v-else>
          <b>Edit Song:</b> We found this song in the music4dance catalog, please vote on dance
          styles and add tags to improve the catalog.
        </div>
      </BAlert>
      <SongCore
        :model="songModel as SongDetailsModel"
        :start-editing="true"
        :creating="!!songModel && (songModel as SongDetailsModel).created"
        @song-saved="reset(true)"
        @cancel-changes="reset(false)"
      />
    </div>
    <BRow
      ><BCol>
        <BAlert :model-value="true" variant="warning">
          Adding songs is a new feature so if you run into bugs or have suggestions for improving
          this feature please don't hesitate to send email to
          <a href="mailto:info@music4dance.net">info@music4dance.net</a> or fill out our
          <a href="https://music4dance.blog/feedback/">feedback form</a> and we'll be happy to take
          your input as we contineu to improve the feature.
        </BAlert>
      </BCol>
    </BRow>
  </PageFrame>
</template>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
