<script setup lang="ts">
import { ServiceMatcher } from "@/helpers/ServiceMatcher";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { computed, onMounted, ref } from "vue";
import type { SongDetailsModel } from "@/models/SongDetailsModel";

const context = getMenuContext();

const props = defineProps<{ id?: string }>();

const emit = defineEmits<{
  "edit-song": [song: SongDetailsModel];
}>();

const serviceString = ref(props.id ?? "");
const songId = ref("");
const searching = ref(false);
const failed = ref(false);
const serviceMatcher = new ServiceMatcher();

const serviceIdState = computed(() => {
  return serviceString.value ? !!serviceId.value : null;
});

const service = computed(() => {
  return serviceMatcher.match(serviceString.value);
});

const serviceId = computed(() => {
  return service.value ? serviceMatcher.parseId(serviceString.value, service.value) : null;
});

const serviceType = computed(() => {
  return service.value ? service.value.id : null;
});

const serviceName = computed(() => {
  return service.value ? service.value.name : null;
});

const onCancel = (): void => {
  searching.value = false;
  failed.value = false;
};

const lookupService = async (s: string): Promise<void> => {
  searching.value = true;
  try {
    const song = await serviceMatcher.findSong(s);
    if (song) {
      emit("edit-song", song);
    } else {
      failed.value = true;
    }
  } catch {
    failed.value = true;
  } finally {
    searching.value = false;
  }
};

const findService = async (): Promise<void> => {
  if (serviceString.value) {
    await lookupService(serviceString.value);
  }
};

const checkService = async (s: string): Promise<void> => {
  if (s && serviceMatcher.match(s)) {
    await findService();
  }
};

onMounted(async () => {
  if (serviceString.value) {
    await findService();
  }
});
</script>

<template>
  <div>
    <p>
      Add a song by entering an ID or URL from ITunes or Spotify (or dragging the song into the text
      box) and clicking the <b>Find</b> button
    </p>
    <BForm v-show="!searching" inline>
      <BInputGroup prepend="Service Id">
        <BFormInput
          id="service-id"
          v-model="serviceString"
          palceholder="Apple Music or Spotify Id"
          aria-describedby="service-id-feedback"
          :state="serviceIdState"
          trim
          @update:model-value="checkService"
        ></BFormInput>
        <BButton variant="primary" @click="findService">Find</BButton>
        <BFormInvalidFeedback id="service-id-feedback" :disabled="!serviceId">
          Enter a valid iTunes or Spotify id or url
        </BFormInvalidFeedback>
      </BInputGroup>
      <div v-if="service" class="ml-2">
        {{ serviceName }} id = <b>{{ serviceId }}</b>
      </div>
    </BForm>
    <BAlert :model-value="searching" variant="info">
      <BSpinner class="me-3"></BSpinner>
      <span v-show="!failed">Searching for </span>
      <span v-show="failed">Failed to find </span>
      {{ serviceId }} on music4dance.net and {{ serviceName }}
      <BButton v-show="failed" variant="outline-warning" @click="onCancel"> Cancel</BButton>
    </BAlert>
    <br />
    <AugmentSources></AugmentSources>
    <form v-show="false" id="createSong" ref="createSong" action="/song/create" method="post">
      <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
      <input id="service" type="hidden" name="service" :value="serviceType" />
      <input id="purchase" type="hidden" name="purchase" :value="serviceId" />
    </form>
    <form v-show="false" id="editSong" ref="editSong" action="/song/edit" method="post">
      <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
      <input id="id" type="hidden" name="id" :value="songId" />
    </form>
  </div>
</template>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
