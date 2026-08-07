CC ?= gcc
CFLAGS ?= -O2 -Wall -Wextra
LDFLAGS ?=

ifneq ($(shell pkg-config --exists libseccomp 2>/dev/null && echo yes),)
CFLAGS += -DUSE_SECCOMP $(shell pkg-config --cflags libseccomp)
LDFLAGS += $(shell pkg-config --libs libseccomp)
endif

relic-sandbox: relic-sandbox.c
	$(CC) $(CFLAGS) -o $@ relic-sandbox.c $(LDFLAGS)

clean:
	rm -f relic-sandbox

.PHONY: clean
