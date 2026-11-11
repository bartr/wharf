FROM public.ecr.aws/x8v8d7g8/mars-base:latest

RUN apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y ca-certificates wget \
 && wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    -O /tmp/packages-microsoft-prod.deb \
 && dpkg -i /tmp/packages-microsoft-prod.deb \
 && rm /tmp/packages-microsoft-prod.deb \
 && apt-get update \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-10.0

WORKDIR /app
COPY app/ /app/
RUN dotnet restore wharf.slnx \
 && git init -q \
 && git add -A \
 && GIT_AUTHOR_DATE="2024-01-01T00:00:00Z" GIT_COMMITTER_DATE="2024-01-01T00:00:00Z" \
    git -c user.name="deepswe" -c user.email="deepswe@local" \
        -c commit.gpgsign=false commit -q -m "Initial commit" \
 && git config core.hooksPath /dev/null

CMD ["/bin/bash"]
