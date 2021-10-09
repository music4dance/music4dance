<template>
  <div>
    <p>
      Add a song by entering an ID or URL from ITunes or Spotify and clicking
      the <b>Find</b> button
    </p>
    <b-form inline v-show="!searching">
      <b-input-group>
        <label class="sr-only" for="service-id">Service Id</label>
        <b-form-input
          id="service-id"
          palceholder="Apple Music or Spotify Id"
          v-model="serviceString"
          aria-describedby="service-id-feedback"
          :state="serviceIdState"
          trim
        ></b-form-input>
        <b-input-group-append>
          <b-button variant="primary" @click="findService">Find</b-button>
        </b-input-group-append>
        <b-form-invalid-feedback
          id="service-id-feedback"
          :disabled="!serviceId"
        >
          Enter a valid iTunes or Spotify id or url
        </b-form-invalid-feedback>
      </b-input-group>
      <div v-if="service" class="ml-2">
        {{ serviceName }} id = <b>{{ serviceId }}</b>
      </div>
    </b-form>
    <b-alert v-show="searching" show variant="info">
      <b-spinner class="mr-3"></b-spinner>
      <span v-show="!failed">Searching for </span>
      <span v-show="failed">Failed to find </span>
      {{ serviceId }} on music4dance.net and {{ serviceName }}
      <b-button v-show="failed" variant="outline-warning" @click="onCancel">
        Cancel</b-button
      >
    </b-alert>
    <br />
    <augment-sources></augment-sources>
    <form
      id="createSong"
      ref="createSong"
      action="/song/create"
      method="post"
      v-show="false"
    >
      <input
        type="hidden"
        name="__RequestVerificationToken"
        :value="context.xsrfToken"
      />
      <input type="hidden" name="service" id="service" :value="serviceType" />
      <input type="hidden" name="purchase" id="purchase" :value="serviceId" />
    </form>
    <form
      id="editSong"
      ref="editSong"
      action="/song/edit"
      method="post"
      v-show="false"
    >
      <input
        type="hidden"
        name="__RequestVerificationToken"
        :value="context.xsrfToken"
      />
      <input type="hidden" name="id" id="id" :value="songId" />
    </form>
  </div>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import axios from "axios";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Component, Mixins } from "vue-property-decorator";
import AugmentSources from "./AugmentSources.vue";

interface Service {
  id: string;
  name: string;
  rgx: RegExp[];
}

const services: Service[] = [
  {
    id: "i",
    name: "Apple Music",
    rgx: [
      /(^\d{9})$/gi,
      /https:\/\/(?:music|itunes)\.apple\.com.*\/\d{7,10}\?i=(\d{7,10})/gi,
    ],
  },
  {
    id: "s",
    name: "Spotify",
    rgx: [
      /^([a-z0-9]{22})$/gi,
      /https:\/\/open\.spotify\.com\/track\/([a-z0-9]{22})/gi,
    ],
  },
];

@Component({
  components: { AugmentSources },
})
export default class AugmentLookup extends Mixins(AdminTools) {
  private serviceString = "";
  private songId = "";
  private searching = false;
  private failed = false;

  private get serviceIdState(): boolean | null {
    return this.serviceString ? !!this.serviceId : null;
  }

  private async findService(): Promise<void> {
    this.searching = true;
    try {
      const uri = `/api/servicetrack/${this.serviceType}${this.serviceId}`;
      const response = await axios.get(uri);
      const songModel = TypedJSON.parse(response.data, SongDetailsModel);
      if (!songModel) {
        this.failed = true;
      } else {
        this.$emit("edit-song", songModel);
      }
    } catch (e) {
      this.failed = true;
    }
  }

  private onCancel(): void {
    this.searching = false;
    this.failed = false;
  }

  private get serviceId(): string | null {
    const service = this.service;
    return service ? this.parseId(this.serviceString, service) : null;
  }

  private get serviceType(): string | null {
    const service = this.service;
    return service ? service.id : null;
  }

  private get serviceName(): string | null {
    const service = this.service;
    return service ? service.name : null;
  }

  private get service(): Service | undefined {
    return services.find((s) => this.matchService(this.serviceString, s));
  }

  private matchService(id: string, service: Service): boolean {
    return service.rgx.some((rgx) => id.match(rgx));
  }

  private parseId(id: string, service: Service): string {
    const rgx = service.rgx.find((r) => id.match(r));
    if (!rgx) {
      throw new Error(`Invalid id ${id}: No regex found for ${service.name}`);
    }
    const match = rgx.exec(id);
    if (!match) {
      throw new Error(`Invalid id ${id}: No match found for ${service.name}`);
    }

    return match[1];
  }
}
</script>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
