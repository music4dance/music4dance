<script setup lang="ts">
import { getMenuContext } from "@/helpers/GetMenuContext";
import { SongFilter } from "@/models/SongFilter";
import { SongListModel } from "@/models/SongListModel";
import { ref } from "vue";

// TODO: Consider generalizing bulk edit functionality and creating a component
//  Also, look at generalizing tag capability (this currently won't catch individual
//  tags when the are glommed together in a single property)

const props = defineProps<{
  model: SongListModel;
  selected: string[];
}>();

const context = getMenuContext();

const editAction = ref("");
const editProperties = ref("");
const tempoMultiplier = ref(0.5);

const simpleMerge = ref<HTMLInputElement | null>(null);
const deleteSongs = ref<HTMLInputElement | null>(null);
const cleanupAlbums = ref<HTMLInputElement | null>(null);
const merge = ref<HTMLInputElement | null>(null);

const filter = props.model.filter ?? new SongFilter();
const selectedSongs = props.selected ?? [];

const editUser = context.userName ?? "";

const batchUrl = (name: string, count: number, type?: string, options?: string): string => {
  const typeParam = type ? `type=${type}` : "";
  const optionsParam = options ? `options=${options}` : "";
  const separator = typeParam && optionsParam ? "&" : "";
  return batchUrlBase(name, count, typeParam + separator + optionsParam);
};

const batchUrlBase = (name: string, count: number, additional?: string): string => {
  additional = additional ? `&${additional}` : "";
  return `/song/${name}?count=${count}${additional}&filter=${filter.encodedQuery}`;
};

const onBulkEdit = (submit: HTMLInputElement | null): void => {
  const button = submit as HTMLButtonElement;
  button.click();
};
</script>

<template>
  <BRow v-if="context.isAdmin">
    <BCol>
      <BButtonToolbar aria-label="Admin song modifiers">
        <BDropdown right text="Multi-Edit" class="mx-1 mb-1">
          <BDropdownItem @click="onBulkEdit(simpleMerge)">Simple Merge</BDropdownItem>
          <BDropdownItem @click="onBulkEdit(merge)">Merge</BDropdownItem>
          <BDropdownItem @click="onBulkEdit(deleteSongs)">Delete</BDropdownItem>
          <BDropdownItem @click="onBulkEdit(cleanupAlbums)">Cleanup Albums</BDropdownItem>
        </BDropdown>
        <BDropdown right text="Clean" class="mx-1 mb-1">
          <BDropdownItem :href="batchUrl('batchcleanservice', -1)">Services</BDropdownItem>
          <BDropdownItem :href="batchUrl('batchcleanservice', -1, 'SP')">Spotify</BDropdownItem>
          <BDropdownItem :href="batchUrl('batchcleanupproperties', -1)">Properties</BDropdownItem>
          <BDropdownItem :href="batchUrl('batchreloadsongs', -1)">Reload</BDropdownItem>
          <BDropdownItem :href="batchUrl('checkproperties', -1)">Check Properties</BDropdownItem>
        </BDropdown>
        <BDropdown right text="Download" class="mx-1 mb-1">
          <BDropdownItem :href="batchUrl('downloadJson', -1, 'S')">Songs</BDropdownItem>
          <BDropdownItem :href="batchUrl('downloadJson', -1, 'H')">History</BDropdownItem>
        </BDropdown>
        <BDropdown right text="Update" class="mx-1">
          <BDropdownItem :href="batchUrlBase('batchUpdateService', -1, 'serviceType=I')"
            >iTunes</BDropdownItem
          >
          <BDropdownItem :href="batchUrlBase('batchEchoNest', -1)">EchoNest</BDropdownItem>
          <BDropdownItem :href="batchUrlBase('batchSamples', -1)">Samples</BDropdownItem>
          <BDropdownItem>User Tags (deprecated)</BDropdownItem>
          <BDropdownItem>Tags (deprecated)</BDropdownItem>
          <BDropdownItem>Tag Summaries</BDropdownItem>
        </BDropdown>
      </BButtonToolbar>
      <form v-show="false" id="bulkEdit" action="/song/bulkedit" method="post">
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <input
          v-for="song in selectedSongs"
          :key="song"
          type="hidden"
          name="selectedSongs"
          :value="song"
        />
        <input type="hidden" name="filter" :value="model.filter.query" />
        <input type="submit" name="action" :value="editAction" />
        <input ref="merge" type="submit" name="action" value="Merge" />
        <input ref="simpleMerge" type="submit" name="action" value="SimpleMerge" />
        <input ref="deleteSongs" type="submit" name="action" value="Delete" />
        <input ref="cleanupAlbums" type="submit" name="action" value="cleanupAlbums" />
      </form>
    </BCol>
    <BCol>
      <form
        id="batchAdminEdit"
        action="/song/batchadminedit"
        method="post"
        enctype="multipart/form-data"
      >
        <h3>Bulk Admin Edit</h3>
        <input type="hidden" name="filter" :value="model.filter.query" />
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BFormGroup
          id="bae-user-group"
          label="User Name:"
          label-for="bea-user"
          description="User name to attribute the edits to."
          ><BFormInput id="bea-user" v-model="editUser" name="user" required
        /></BFormGroup>
        <BFormGroup
          id="bae-properties-group"
          label="Properties:"
          label-for="bea-properties"
          description="Properties to append to each song in the current filter."
          ><BFormInput id="bea-properties" v-model="editProperties" name="properties" required
        /></BFormGroup>
        <BButton type="submit">Submit</BButton>
      </form>
    </BCol>
    <BCol>
      <form
        id="batchAdminModify"
        action="/song/batchadminmodify"
        method="post"
        enctype="multipart/form-data"
      >
        <h3>Bulk Admin Modify</h3>
        <input type="hidden" name="filter" :value="model.filter.query" />
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BFormGroup
          id="bme-properties-group"
          label="Song Modifier:"
          label-for="bma-properties"
          description="A SongModifier object in JSON format"
          ><BFormInput id="bma-properties" v-model="editProperties" name="properties" required
        /></BFormGroup>
        <BButton type="submit">Submit</BButton>
      </form>
    </BCol>
    <BCol>
      <form
        id="batchCorrectTempo"
        action="/song/batchcorrecttempo"
        method="post"
        enctype="multipart/form-data"
      >
        <h3>Bulk Correct Tempo</h3>
        <input type="hidden" name="filter" :value="model.filter.query" />
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BFormGroup
          id="bct-multiplier-group"
          label="Tempo Multiplier:"
          label-for="bct-multiplier"
          description="Multiplier to change each song's tempo by"
          ><BFormInput id="bct-multiplier" v-model="tempoMultiplier" name="multiplier" required
        /></BFormGroup>
        <BButton type="submit">Submit</BButton>
      </form>
    </BCol>
  </BRow>
</template>
