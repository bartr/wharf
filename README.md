# Wharf 1.5.0

Wharf 1.5.0 builds on the MVP release of the DeepSWE/Harbor benchmark
execution platform. It provides deterministic DeepSWE task selection, isolated
Copilot agent and verifier execution, durable evidence, recovery, unattended
benchmark operation, HydraFusion support, and reproducible analytics. It runs
[Harbor-format tasks](docs/Harbor.md), which bundle task instructions,
execution environments, and automated verification of the resulting code.

## Running Wharf

- Tested hosts are Ubuntu 24.04 LTS
  - Tested on bare-metal, cloud VM, and WSL2
  - Other Linux versions / distros will work
    - The setup scripts will likely need to be updated

- Clone this repo
  ```bash
  git clone https://github.com/bartr/wharf

  # start in the repo root
  cd wharf
  ```

- Save GitHub Copilot PAT to `.wharf`
  ```bash
  mkdir -p $HOME/.wharf
  printf %s "github_pat_for_copilot_..." > $HOME/.wharf/copilot

  # set access
  chmod 0700 $HOME/.wharf
  chmod 0600 $HOME/.wharf/*
  ```

- Install pre-requisites
  ```bash
  # base.sh assumes a stock Ubunutu install - adjust as needed
  sudo ./setup/base.sh

  # ensure $HOME/bin is in your path
  ./setup/wharf.sh
  ```
- Run `wharf check` to validate setup
    ```bash
    wharf check
    ```
- Run smoke test
   ```bash
   cd wharf-swe
   wharf smoke
   ```
- Prebuild the docker image cache
   ```bash
   # prebuild help
   wharf prebuild -h

   # adjust -p 12 based on your disk IOPs
   wharf prebuild -p 12
   ```

- Run a WharfSWE test
  ```bash
  # run help
  wharf run -h

  # adjust -n 16 based on the number of vCPUs - 2 * vCPUs is a good starting point
  # adjust -p 12 based on IOPs
  # adjust -k 4 based on the number of 14 test runs
  #   DeepSWE standards are 4-5 iterations of all tests
  wharf run -n 16 -p 12 -k 4 --model gpt-6-luna
  ```

## Running DeepSWE with Wharf

```bash
# make sure you're in the repoRoot/deep-swe directory
cd ../deep-swe
```
```bash
# run smoke test
wharf smoke
```

```bash
# fill the docker cache (113 * 2 images to cache - it takes about 8 min)
wharf prebuild -p 12
```

```bash
# run the test - this takes 4-10 hours depending on -n, -k and the model
# luna is the least expensive model
wharf run -n 16 -k 4 -p 12 --model gpt-6-luna
```

## References

- [Harbor test process](docs/Harbor.md),
- [DeepSWE results](docs/RESULTS.md), [analytics contract](docs/ANALYTICS.md), and
- [WharfSWE summary](wharf-swe/README.md). The datasets remain separate.
