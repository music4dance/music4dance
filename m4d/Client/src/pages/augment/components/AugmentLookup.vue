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
import { Component, Mixins } from "vue-property-decorator";
import AugmentSources from "./AugmentSources.vue";

@Component({
  components: { AugmentSources },
})
export default class AugmentLookup extends Mixins(AdminTools) {
  private serviceString = "";
  private songId = "";
  private searching = false;
  private failed = false;
  private serviceMatcher = new ServiceMatcher();

  private get serviceIdState(): boolean | null {
    return this.serviceString ? !!this.serviceId : null;
  }

  private async findService(): Promise<void> {
    this.searching = true;
    const song = await this.serviceMatcher.findSong(this.serviceString);
    if (song) {
      this.$emit("edit-song", song);
    } else {
      this.failed = true;
    }
  }

  private onCancel(): void {
    this.searching = false;
    this.failed = false;
  }

  private get service(): Service | undefined {
    return this.serviceMatcher.match(this.serviceString);
  }

  private get serviceId(): string | null {
    const service = this.service;
    return service
      ? this.serviceMatcher.parseId(this.serviceString, service)
      : null;
  }

  private get serviceType(): string | null {
    const service = this.service;
    return service ? service.id : null;
  }

  private get serviceName(): string | null {
    const service = this.service;
    return service ? service.name : null;
  }
}
</script>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
