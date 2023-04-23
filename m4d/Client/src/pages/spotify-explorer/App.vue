<template>
  <page id="app" title="Spotify Explorer">
    <p>
      View a Spotify track, playlist or user by entering a spotify url or
      dragging and dropping a link from the Spotify player
      <b>Find</b> button
    </p>
    <b-form ref="form" inline autocomplete="false">
      <b-input-group>
        <label class="sr-only" for="service-id">Service Id</label>
        <b-form-input
          id="service-id"
          palceholder="Spotify Playlist Id"
          v-model="serviceString"
          aria-describedby="service-id-feedback"
          :state="serviceIdState"
          trim
          class="mr-2"
          @input="find"
        ></b-form-input>
        <b-input-group-append class="mr-2">
          <b-button variant="primary" @click="find">Find</b-button>
        </b-input-group-append>
        <b-input-group-append>
          <b-button @click="clearForm">Clear</b-button>
        </b-input-group-append>
        <b-form-invalid-feedback
          id="service-id-feedback"
          :disabled="!serviceId"
        >
          Enter a valid Spotify Playist ID or Link
        </b-form-invalid-feedback>
      </b-input-group>
      <div v-if="serviceId" class="ml-2">
        Spotify id = <b>{{ serviceId }}</b> Type =
        <b>{{ serviceKindString }}</b>
      </div>
    </b-form>
    <loader v-if="searching || loaded" :loaded="loaded">
      <playlist-viewer
        v-if="playlist"
        class="mt-2"
        :id="serviceId"
        :model="playlist"
      ></playlist-viewer>
      <service-user-viewer
        v-if="serviceUser"
        class="mt-2"
        :id="serviceId"
        :model="serviceUser"
      ></service-user-viewer>
    </loader>
  </page>
</template>

<script lang="ts">
import mixins from "vue-typed-mixins";
import Page from "@/components/Page.vue";
import AdminTools from "@/mix-ins/AdminTools";
import PlaylistViewer from "./components/PlaylistViewer.vue";
import ServiceUserViewer from "./components/ServiceUserViewer.vue";
import Loader from "@/components/Loader.vue";
import { PlaylistModel } from "@/model/PlaylistModel";
import { ServiceMatcher } from "@/model/ServiceMatcher";
import "reflect-metadata";
import DropTarget from "@/mix-ins/DropTarget";
import { ServiceUser } from "@/model/ServiceUser";

enum ServiceObjectKind {
  None = 0,
  Track,
  Playlist,
  User,
}

interface ServiceObjectDescription {
  kind: ServiceObjectKind;
  id: string;
}

export default mixins(AdminTools, DropTarget).extend({
  components: {
    Page,
    PlaylistViewer,
    ServiceUserViewer,
    Loader,
  },
  data() {
    return new (class {
      serviceString = "";
      searching = false;
      failed = false;
      serviceMatcher = new ServiceMatcher();
      playlist: PlaylistModel | null = null;
      serviceUser: ServiceUser | null = null;
    })();
  },
  computed: {
    serviceIdState(): boolean | null {
      return this.serviceString ? !!this.serviceId : null;
    },

    serviceId(): string | null {
      const description = this.serviceDescription;
      return description ? description.id : null;
    },

    serviceKind(): ServiceObjectKind {
      const description = this.serviceDescription;
      return description ? description.kind : ServiceObjectKind.None;
    },

    serviceKindString(): string {
      return ServiceObjectKind[this.serviceKind];
    },

    serviceDescription(): ServiceObjectDescription | null {
      const serviceString = this.serviceString;
      let id = this.serviceMatcher.parsePlaylist(serviceString);
      if (id) {
        return { kind: ServiceObjectKind.Playlist, id };
      }
      id = this.serviceMatcher.parseUser(serviceString);
      if (id) {
        return { kind: ServiceObjectKind.User, id };
      }
      const service = this.serviceMatcher.match(serviceString);
      if (service) {
        id = this.serviceMatcher.parseId(serviceString, service);
        if (id) {
          return { kind: ServiceObjectKind.Track, id };
        }
      }
      return null;
    },
    loaded(): boolean {
      return !!this.playlist || !!this.serviceUser;
    },
  },
  methods: {
    async find(): Promise<void> {
      const serviceString = this.serviceString;
      switch (this.serviceKind) {
        case ServiceObjectKind.None:
          break;
        case ServiceObjectKind.Playlist:
          await this.findPlaylist();
          break;
        case ServiceObjectKind.User:
          await this.findUser();
          break;
        case ServiceObjectKind.Track:
          this.checkServiceAndAdd(serviceString);
          break;
        default:
          throw new Error(`Invalid Service Kind: {this.serviceKind}`);
      }
    },
    async findPlaylist(): Promise<void> {
      this.searching = true;
      const playlist = await this.serviceMatcher.findSpotifyPlaylist(
        this.serviceString
      );
      if (playlist) {
        this.playlist = playlist;
      } else {
        this.failed = true;
      }
      this.searching = false;
    },
    async findUser(): Promise<void> {
      this.searching = true;
      const user = await this.serviceMatcher.findSpotifyUser(
        this.serviceString
      );
      if (user) {
        this.serviceUser = user;
      } else {
        this.failed = true;
      }
      this.searching = false;
    },
    clearForm(): void {
      this.serviceString = "";
      this.failed = false;
      this.searching = false;
      this.playlist = null;
      this.serviceUser = null;
      const form = this.$refs["form"] as HTMLFormElement;
      form.reset();
    },
  },
  mounted(): void {
    const params = new URLSearchParams(window.location.search);
    const link = params.get("link");
    if (link) {
      this.serviceString = link;
      this.find();
    }
  },
});
</script>
