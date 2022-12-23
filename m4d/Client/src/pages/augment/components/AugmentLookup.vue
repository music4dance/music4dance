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
import { Service, ServiceMatcher } from "@/model/ServiceMatcher";
import "reflect-metadata";
import AugmentSources from "./AugmentSources.vue";

export default AdminTools.extend({
  components: { AugmentSources },
  props: { id: String },
  data() {
    return new (class {
      serviceString = "";
      songId = "";
      searching = false;
      failed = false;
      serviceMatcher = new ServiceMatcher();
    })();
  },
  computed: {
    serviceIdState(): boolean | null {
      return this.serviceString ? !!this.serviceId : null;
    },
    service(): Service | undefined {
      return this.serviceMatcher.match(this.serviceString);
    },

    serviceId(): string | null {
      const service = this.service;
      return service
        ? this.serviceMatcher.parseId(this.serviceString, service)
        : null;
    },

    serviceType(): string | null {
      const service = this.service;
      return service ? service.id : null;
    },

    serviceName(): string | null {
      const service = this.service;
      return service ? service.name : null;
    },
  },
  methods: {
    async findService(): Promise<void> {
      this.searching = true;
      const song = await this.serviceMatcher.findSong(this.serviceString);
      this.serviceString = "";
      if (song) {
        this.$emit("edit-song", song);
      } else {
        this.failed = true;
      }
    },

    onCancel(): void {
      this.searching = false;
      this.failed = false;
    },
  },
  async mounted(): Promise<void> {
    this.serviceString = this.id ?? "";
    if (this.serviceString) {
      await this.findService();
    }
  },
});
</script>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
