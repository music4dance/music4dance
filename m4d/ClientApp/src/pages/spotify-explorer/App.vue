<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import PlaylistViewer from "./components/PlaylistViewer.vue";
import ServiceUserViewer from "./components/ServiceUserViewer.vue";
import SpinLoader from "@/components/SpinLoader.vue";
import { ServiceMatcher } from "@/helpers/ServiceMatcher";
import { PlaylistModel } from "@/models/PlaylistModel";
import { ServiceUser } from "@/models/ServiceUser";
import { useDropTarget } from "@/composables/useDropTarget";
import { computed, onMounted, ref } from "vue";

const { checkServiceAndWarn } = useDropTarget();

enum ServiceObjectKind {
  None = 0,
  Track,
  Playlist,
  User,
}

const serviceString = ref("");
const searching = ref(false);
const failed = ref(false);
const playlist = ref<PlaylistModel | null>(null);
const serviceUser = ref<ServiceUser | null>(null);
const serviceMatcher = new ServiceMatcher();

const serviceDescription = computed(() => {
  let id = serviceMatcher.parsePlaylist(serviceString.value);
  if (id) {
    return { kind: ServiceObjectKind.Playlist, id };
  }
  id = serviceMatcher.parseUser(serviceString.value);
  if (id) {
    return { kind: ServiceObjectKind.User, id };
  }
  const service = serviceMatcher.match(serviceString.value);
  if (service) {
    id = serviceMatcher.parseId(serviceString.value, service);
    if (id) {
      return { kind: ServiceObjectKind.Track, id };
    }
  }
  return null;
});

const serviceId = computed(() => {
  const description = serviceDescription.value;
  return description ? description.id : null;
});

const serviceIdState = computed(() => {
  return serviceString.value ? !!serviceId.value : null;
});

const serviceKind = computed(() => {
  const description = serviceDescription.value;
  return description ? description.kind : ServiceObjectKind.None;
});

const serviceKindString = computed(() => {
  return ServiceObjectKind[serviceKind.value];
});

const loaded = computed(() => {
  return !!playlist.value || !!serviceUser.value;
});

const findPlaylist = async (): Promise<void> => {
  searching.value = true;
  const pl = await serviceMatcher.findSpotifyPlaylist(serviceString.value);
  if (pl) {
    playlist.value = pl;
  } else {
    failed.value = true;
  }
  searching.value = false;
};

const findUser = async (): Promise<void> => {
  searching.value = true;
  const user = await serviceMatcher.findSpotifyUser(serviceString.value);
  if (user) {
    serviceUser.value = user;
  } else {
    failed.value = true;
  }
  searching.value = false;
};

const find = async (): Promise<void> => {
  switch (serviceKind.value) {
    case ServiceObjectKind.None:
      break;
    case ServiceObjectKind.Playlist:
      await findPlaylist();
      break;
    case ServiceObjectKind.User:
      await findUser();
      break;
    case ServiceObjectKind.Track:
      checkServiceAndWarn(serviceString.value);
      break;
    default:
      throw new Error(`Invalid Service Kind: {serviceKind.value}`);
  }
};

const clearForm = (): void => {
  serviceString.value = "";
  failed.value = false;
  searching.value = false;
  playlist.value = null;
  serviceUser.value = null;
  const form = document.getElementById("form") as HTMLFormElement;
  form.reset();
};

onMounted(async () => {
  const params = new URLSearchParams(window.location.search);
  const link = params.get("link");
  if (link) {
    serviceString.value = link;
    find();
  }
});

// TODONEXT: Get this working
</script>

<template>
  <PageFrame id="app" title="Spotify Explorer">
    <p>
      View a Spotify track, playlist or user by entering a spotify url or dragging and dropping a
      link from the Spotify player
      <b>Find</b> button
    </p>
    <BForm ref="form" inline autocomplete="false">
      <BInputGroup>
        <BFormText class="sr-only me-2" for="service-id">Service Id</BFormText>
        <BFormInput
          id="service-id"
          v-model="serviceString"
          palceholder="Spotify Playlist Id"
          aria-describedby="service-id-feedback"
          :state="serviceIdState"
          trim
          prepend="Service ID"
          class="mr-2"
          @input="find"
        ></BFormInput>
        <BButton variant="primary" @click="find">Find</BButton>
        <BButton @click="clearForm">Clear</BButton>
        <BFormInvalidFeedback id="service-id-feedback" :disabled="!serviceId">
          Enter a valid Spotify Playist ID or Link
        </BFormInvalidFeedback>
      </BInputGroup>
      <div v-if="serviceId" class="ml-2">
        Spotify id = <b>{{ serviceId }}</b> Type =
        <b>{{ serviceKindString }}</b>
      </div>
    </BForm>
    <SpinLoader v-if="searching || loaded" :loaded="loaded">
      <PlaylistViewer
        v-if="playlist"
        :id="serviceId!"
        class="mt-2"
        :model="playlist"
      ></PlaylistViewer>
      <ServiceUserViewer
        v-if="serviceUser"
        :id="serviceId"
        class="mt-2"
        :model="serviceUser"
      ></ServiceUserViewer>
    </SpinLoader>
  </PageFrame>
</template>
