FROM public.ecr.aws/x8v8d7g8/mars-base:latest

ENV PATH="/go/bin:/root/go/bin:${PATH}"
WORKDIR /app

# Local-source task (no clone): the project ships in this build context under
# app/ and is COPYed straight into /app, then turned into a git repo with a
# single, DETERMINISTIC base commit. Fixed identity + author/committer dates +
# message => reproducible SHA 62b380d84cd04ad32ff8056a59fe9c57b2601a21, which is
# the preimage the verifier resets to and pre_artifacts.sh diffs against.
COPY app/ /app/
RUN git init -q \
 && git add -A \
 && GIT_AUTHOR_DATE="2024-01-01T00:00:00Z" GIT_COMMITTER_DATE="2024-01-01T00:00:00Z" \
    git -c user.name="deepswe" -c user.email="deepswe@local" -c commit.gpgsign=false \
        commit -q -m "Initial commit" \
 && git config core.hooksPath /dev/null \
 && go mod download

CMD ["/bin/bash"]
