<script setup lang="ts">
import { type BreadCrumbItem, infoTrail } from "@/models/BreadCrumbItem";

const breadcrumbs: BreadCrumbItem[] = [
  ...infoTrail,
  { text: "About", href: "/home/about" },
  { text: "Resume", active: true },
];
</script>

<template>
  <PageFrame id="app" title="David's Resume" :breadcrumbs="breadcrumbs">
    <p>
      <a href="/Content/DavidGray-Resume.pdf" target="_blank">Printable Version</a>
    </p>
    <div class="title">David W. Gray</div>
    <div class="section">Skills Profile</div>
    <ul>
      <li>Leadership experience using both Agile and Waterfall methodologies</li>
      <li>Mentor and coach</li>
      <li>C#/.Net, C/C++, JavaScript, TypeScript, Vue.js, MySql, T-SQL, Azure AI Search</li>
      <li>Full Stack Web, Desktop Application, Diagnostic Tools Development</li>
    </ul>
    <div class="section">Employment History</div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Founder, music4dance.net</div>
        <div class="dates">2014 — Present</div>
      </div>
      <div class="location">Seattle, WA</div>
      <div class="description">
        <ul>
          <li>
            Built a ratings and referral website that helps dancers find music that inspires them to
            dance: A Web application written in ASP.NET Core with a Vue.js front end, running in the
            Azure cloud and built around Azure AI search.
          </li>
          <li>
            This project provides me with the opportunity to work with new technologies and explore
            upgrade paths in a fully controlled sandbox. I originally developed music4dance in
            ASP.NET MVC 3 and Microsoft T-SQL. Early on, I started sprinkling knockout.js code to
            make the UI more reactive. I've kept up with the .NET upgrades, including the major .NET
            core shift, converted the core Database to Azure Cognitive Search, and all the pages
            that require a significant amount of JavaScript to Vue.js/Typescript.
          </li>
        </ul>
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">
          Maintainer,
          <a href="https://github.com/bootstrap-vue-next/bootstrap-vue-next" target="_blank"
            >BootstrapVueNext</a
          >
        </div>
        <div class="dates">2025 — Present</div>
      </div>
      <div class="location">Remote</div>
      <div class="description">
        <ul>
          <li>
            When the maintainers of the front-end library that music4dance depends on (<a
              href="https://bootstrap-vue.org/"
              target="_blank"
              >bootstrap-vue</a
            >) stepped back, I joined the engineers working on a replacement. I've contributed to
            the architecture, made numerous bug fixes, added and reworked several components, built
            out the documentation, and developed a parity report to track our progress. Using AI to
            web-scrape the bootstrap-vue documentation to generate the parity report and writing a
            VitePress Plugin/Markdown Extension to generate running sample code and annotated
            samples from the same source in the documentation were just two of the more technically
            interesting challenges.
          </li>
        </ul>
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Principal Lead Software Engineer, Microsoft</div>
        <div class="dates">2003 — 2012</div>
      </div>
      <div class="location">Redmond, WA</div>
      <div class="description">
        <ul>
          <li>
            Engineering lead for the Visual Studio Debugger, Profiler, and IntelliTrace. Drove
            best-of-class diagnostics tools through multiple product cycles. Attacked many of the
            most complex problems developers face, including reducing the "not reproducible" problem
            and diagnosing issues in the cloud, desktop, and devices.
          </li>
          <li>
            Collaborated with cross-functional teams across Microsoft, including Visual Studio, C#,
            VB, C++, .NET, SharePoint, and Windows to enable features that were business critical to
            those teams and to enable the tools that my team owned to work effectively with those
            platforms and languages.
          </li>
          <li>Worked with management and the team to transition to a SCRUM process.</li>
        </ul>
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">President, theGray.com</div>
        <div class="dates">1999 — 2003</div>
      </div>
      <div class="location">Seattle, WA</div>
      <div class="description">
        General software consulting, including technical feasibility analysis for venture
        capitalists, website design and deployment, custom office automation and billing software
        for a clinic, web-based survey, and data cleaning.
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Vice President of Technology, Sunhawk.com</div>
        <div class="dates">1996 — 1998</div>
      </div>
      <div class="location">Seattle, WA</div>
      <div class="description">
        Managed a cross-functional team of Software Engineers, Graphics Artists, and Music
        Librarians to build an e-commerce solution for browsing and purchasing sheet music over the
        internet. This solution included a website engine that both compiled static pages served by
        a *nix server and an IIS plugin to dynamically serve pages based on a custom music database,
        as well as a system to download encrypted sheet music and purchase the encryption key by
        processing credit card transactions.
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Lead Software Engineer, Microsoft</div>
        <div class="dates">1995 — 1996</div>
      </div>
      <div class="location">Redmond, WA</div>
      <div class="description">
        Led a team that translated some of the most compelling ideas from Visual Basic Enterprise
        Edition, including database management and component wizards, into the first Enterprise
        Edition of Visual C++.
      </div>
    </div>
    <div class="job">
      <div class="job-row">
        <div class="job-title">Senior Software Engineer, Microsoft</div>
        <div class="dates">1989 — 1995</div>
      </div>
      <div class="location">Redmond, WA</div>
      <div class="description">
        Designed and implemented the core debugger to enable it to target multiple operating systems
        running different microprocessors, locally and remotely while hosted in different
        development environments.
      </div>
    </div>
    <div class="section">Education</div>
    <ul>
      <li>B.S. Applied Mathematics and Computer Science, Carnegie Mellon University, Pittsburgh</li>
      <li>User Experience and Design Certificate, General Assembly, Seattle</li>
    </ul>
    <div class="section">Teaching/Volunteering</div>
    <ul>
      <li>
        Mentor, Computer science students in non-traditional colleges,
        <a href="https://mentorsintech.com/mentors" target="_blank">MinT</a>
      </li>
      <li>
        Team lead for student capstone project,
        <a href="https://mentorsintech.com/open-source-1" target="_blank">CodeDay.org</a>,
        <a href="https://www.codeday.org/volunteer" target="_blank">CodeDay.org</a>, Green River
        College
      </li>
      <li>
        Mentor, MicroInternship
        <a href="https://mentorsintech.com/open-source-1" target="_blank">CodeDay.org</a>
      </li>
      <li>
        General Volunteering, Website and SEO consulting,
        <a href="https://hugohouse.org/" target="_blank">Hugo House</a>
      </li>
      <li>Instructor, Discrete Math and Data Structures, Northeastern University, Seattle</li>
      <li>Volunteer Teacher, Introduction to Computer Science, Truman High School, Federal Way</li>
      <li>
        Instructor, Introduction to Programming Workshop, General Assembly, Seattle, Washington
      </li>
    </ul>
    <div class="section">Writing</div>
    <ul>
      <li>
        Technical Blog:
        <a href="https://medium.com/@dwgray/" target="_blank">https://medium.com/@dwgray/</a>
      </li>
      <li>
        Marketing Blog:
        <a href="https://music4dance.blog/" target="_blank">https://music4dance.blog/</a>
      </li>
    </ul>
  </PageFrame>
</template>

<style lang="scss" scoped>
.title {
  font-size: 3rem;
  font-weight: bold;
}
.section {
  font-size: 2rem;
  font-weight: lighter;
}
.job-title {
  font-weight: bold;
  font-size: 1.25em;
}
.job-row {
  display: flex;
  justify-content: space-between;
}
.location {
  font-weight: lighter;
}
.dates {
  font-weight: lighter;
}
.job {
  margin-bottom: 0.5em;
}
.description {
  font-size: 1.25em;
}
li {
  list-style-type: square;
  font-size: 1.25em;
}
p {
  font-size: 1.25em;
}
</style>
