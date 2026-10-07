# Session 18 Research

- Wharf packages are versioned Debian artifacts built from the single
  `WharfVersion` MSBuild property.
- The package must carry the executable, bootstrap integration, and operator
  documentation, with checksums and an SBOM.
- Host-changing bootstrap operations must use explicit `sudo`; detached GPG
  signing is optional locally but verifiable when configured.
