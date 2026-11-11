FROM public.ecr.aws/x8v8d7g8/mars-base:latest

ENV PATH="/go/bin:/root/go/bin:${PATH}"
WORKDIR /app

# Verifier image (separate mode): tests/ is the build context and the agent
# never sees this container. Rebuild the SAME pristine /app the agent had — the
# local source is duplicated here under app/ (byte-identical to
# environment/app/) so the deterministic base commit hash matches, letting the
# agent's model.patch apply cleanly.
COPY app/ /app/
RUN git init -q \
 && git add -A \
 && GIT_AUTHOR_DATE="2024-01-01T00:00:00Z" GIT_COMMITTER_DATE="2024-01-01T00:00:00Z" \
    git -c user.name="deepswe" -c user.email="deepswe@local" -c commit.gpgsign=false \
        commit -q -m "Initial commit" \
 && git config core.hooksPath /dev/null \
 && go mod download

# CTRF reporter for `go test -json` (resolved at BUILD time; runtime is offline).
RUN go install github.com/ctrf-io/go-ctrf-json-reporter/cmd/go-ctrf-json-reporter@v0.1.0

COPY test.sh /tests/test.sh
COPY grader.py /tests/grader.py
COPY config.json /tests/config.json
COPY test.patch /tests/test.patch
RUN chmod +x /tests/test.sh

CMD ["/bin/bash"]
