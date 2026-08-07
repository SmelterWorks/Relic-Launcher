/*
 * relic-sandbox: apply Landlock and seccomp-bpf then exec.
 * Build: make (requires libseccomp on Linux)
 */
#include <errno.h>
#include <fcntl.h>
#include <linux/landlock.h>
#include <linux/seccomp.h>
#include <signal.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/prctl.h>
#include <sys/syscall.h>
#include <unistd.h>

#ifndef LANDLOCK_ACCESS_NET_BIND_UDP
#define LANDLOCK_ACCESS_NET_BIND_UDP (1ULL << 6)
#endif
#ifndef LANDLOCK_ACCESS_NET_CONNECT_SEND_UDP
#define LANDLOCK_ACCESS_NET_CONNECT_SEND_UDP (1ULL << 7)
#endif
#ifndef O_PATH
#define O_PATH 010000000
#endif

#ifdef USE_SECCOMP
#include <seccomp.h>
#endif

#define MAX_PATHS 256
#define MAX_NET 32

typedef struct {
    char path[4096];
    uint64_t access;
} path_rule_t;

typedef struct {
    uint16_t port;
    uint64_t access;
} net_rule_t;

static path_rule_t paths[MAX_PATHS];
static size_t path_count = 0;
static net_rule_t nets[MAX_NET];
static size_t net_count = 0;
static bool scope_abstract = false;
static bool scope_signal = false;
static int seccomp_profile = 0;
static int max_abi = 9;

static int landlock_create_ruleset(const struct landlock_ruleset_attr *attr,
                                   size_t size, uint32_t flags) {
    return syscall(__NR_landlock_create_ruleset, attr, size, flags);
}

static int landlock_add_rule(int ruleset_fd, enum landlock_rule_type rule_type,
                             const void *rule_attr, uint32_t flags) {
    return syscall(__NR_landlock_add_rule, ruleset_fd, rule_type, rule_attr,
                    flags);
}

static int landlock_restrict_self(int ruleset_fd, uint32_t flags) {
    return syscall(__NR_landlock_restrict_self, ruleset_fd, flags);
}

static void trim(char *s) {
    size_t n = strlen(s);
    while (n > 0 && (s[n - 1] == '\n' || s[n - 1] == '\r' || s[n - 1] == ' '))
        s[--n] = '\0';
}

static uint64_t mask_abi(uint64_t handled, int abi) {
    if (abi < 2)
        handled &= ~LANDLOCK_ACCESS_FS_REFER;
    if (abi < 3)
        handled &= ~LANDLOCK_ACCESS_FS_TRUNCATE;
    if (abi < 4)
        handled &= ~(LANDLOCK_ACCESS_NET_BIND_TCP |
                       LANDLOCK_ACCESS_NET_CONNECT_TCP);
    if (abi < 5)
        handled &= ~LANDLOCK_ACCESS_FS_IOCTL_DEV;
    if (abi < 6)
        handled &= ~(LANDLOCK_SCOPE_ABSTRACT_UNIX_SOCKET |
                     LANDLOCK_SCOPE_SIGNAL);
    if (abi < 9)
        handled &= ~LANDLOCK_ACCESS_FS_RESOLVE_UNIX;
    if (abi < 10)
        handled &= ~(LANDLOCK_ACCESS_NET_BIND_UDP |
                     LANDLOCK_ACCESS_NET_CONNECT_SEND_UDP);
    return handled;
}

static int parse_policy(const char *path) {
    FILE *f = fopen(path, "r");
    if (!f)
        return -1;
    char line[8192];
    while (fgets(line, sizeof(line), f)) {
        trim(line);
        if (line[0] == '\0' || line[0] == '#')
            continue;
        if (strncmp(line, "kind=", 5) == 0)
            continue;
        if (strncmp(line, "scope_abstract=", 15) == 0) {
            scope_abstract = atoi(line + 15) != 0;
            continue;
        }
        if (strncmp(line, "scope_signal=", 13) == 0) {
            scope_signal = atoi(line + 13) != 0;
            continue;
        }
        if (strncmp(line, "seccomp=", 8) == 0) {
            seccomp_profile = atoi(line + 8);
            continue;
        }
        if (strncmp(line, "max_abi=", 8) == 0) {
            max_abi = atoi(line + 8);
            continue;
        }
        if (strncmp(line, "NET_BIND_TCP ", 13) == 0) {
            if (net_count < MAX_NET) {
                nets[net_count].port = (uint16_t)atoi(line + 13);
                nets[net_count].access = LANDLOCK_ACCESS_NET_BIND_TCP;
                net_count++;
            }
            continue;
        }
        if (strncmp(line, "NET_CONNECT_TCP ", 16) == 0) {
            if (net_count < MAX_NET) {
                nets[net_count].port = (uint16_t)atoi(line + 16);
                nets[net_count].access = LANDLOCK_ACCESS_NET_CONNECT_TCP;
                net_count++;
            }
            continue;
        }
        if (strncmp(line, "NET_BIND_UDP ", 13) == 0) {
            if (net_count < MAX_NET) {
                nets[net_count].port = (uint16_t)atoi(line + 13);
                nets[net_count].access = LANDLOCK_ACCESS_NET_BIND_UDP;
                net_count++;
            }
            continue;
        }
        if (strncmp(line, "NET_CONNECT_UDP ", 16) == 0) {
            if (net_count < MAX_NET) {
                nets[net_count].port = (uint16_t)atoi(line + 16);
                nets[net_count].access = LANDLOCK_ACCESS_NET_CONNECT_SEND_UDP;
                net_count++;
            }
            continue;
        }
        if (path_count >= MAX_PATHS)
            continue;
        uint64_t access = LANDLOCK_ACCESS_FS_READ_FILE | LANDLOCK_ACCESS_FS_READ_DIR;
        char *p = line;
        if (strncmp(p, "RW ", 3) == 0) {
            access = LANDLOCK_ACCESS_FS_READ_FILE | LANDLOCK_ACCESS_FS_READ_DIR |
                     LANDLOCK_ACCESS_FS_WRITE_FILE | LANDLOCK_ACCESS_FS_TRUNCATE |
                     LANDLOCK_ACCESS_FS_REMOVE_FILE | LANDLOCK_ACCESS_FS_REMOVE_DIR |
                     LANDLOCK_ACCESS_FS_MAKE_REG | LANDLOCK_ACCESS_FS_MAKE_DIR |
                     LANDLOCK_ACCESS_FS_REFER;
            p += 3;
        } else if (strncmp(p, "RX ", 3) == 0) {
            access = LANDLOCK_ACCESS_FS_EXECUTE | LANDLOCK_ACCESS_FS_READ_FILE |
                     LANDLOCK_ACCESS_FS_READ_DIR;
            p += 3;
        } else if (strncmp(p, "RO ", 3) == 0) {
            p += 3;
        } else {
            continue;
        }
        strncpy(paths[path_count].path, p, sizeof(paths[path_count].path) - 1);
        paths[path_count].access = access;
        path_count++;
    }
    fclose(f);
    return 0;
}

static int apply_landlock(void) {
    int abi = landlock_create_ruleset(NULL, 0, LANDLOCK_CREATE_RULESET_VERSION);
    if (abi < 0)
        return 0;

    if (abi > max_abi)
        abi = max_abi;

    uint64_t handled_fs =
        LANDLOCK_ACCESS_FS_EXECUTE | LANDLOCK_ACCESS_FS_WRITE_FILE |
        LANDLOCK_ACCESS_FS_READ_FILE | LANDLOCK_ACCESS_FS_READ_DIR |
        LANDLOCK_ACCESS_FS_REMOVE_DIR | LANDLOCK_ACCESS_FS_REMOVE_FILE |
        LANDLOCK_ACCESS_FS_MAKE_REG | LANDLOCK_ACCESS_FS_MAKE_DIR |
        LANDLOCK_ACCESS_FS_REFER | LANDLOCK_ACCESS_FS_TRUNCATE |
        LANDLOCK_ACCESS_FS_IOCTL_DEV | LANDLOCK_ACCESS_FS_RESOLVE_UNIX;
    uint64_t handled_net =
        LANDLOCK_ACCESS_NET_BIND_TCP | LANDLOCK_ACCESS_NET_CONNECT_TCP |
        LANDLOCK_ACCESS_NET_BIND_UDP | LANDLOCK_ACCESS_NET_CONNECT_SEND_UDP;
    uint64_t scoped = 0;
    if (scope_abstract)
        scoped |= LANDLOCK_SCOPE_ABSTRACT_UNIX_SOCKET;
    if (scope_signal)
        scoped |= LANDLOCK_SCOPE_SIGNAL;

    handled_fs = mask_abi(handled_fs, abi);
    handled_net = mask_abi(handled_net, abi);

    struct landlock_ruleset_attr attr = {
        .handled_access_fs = handled_fs,
        .handled_access_net = handled_net,
        .scoped = scoped,
    };

    int rs = landlock_create_ruleset(&attr, sizeof(attr), 0);
    if (rs < 0)
        return -1;

    for (size_t i = 0; i < path_count; i++) {
        int fd = open(paths[i].path, O_PATH | O_CLOEXEC);
        if (fd < 0)
            continue;
        struct landlock_path_beneath_attr pb = {
            .allowed_access = paths[i].access & handled_fs,
            .parent_fd = fd,
        };
        if (pb.allowed_access)
            landlock_add_rule(rs, LANDLOCK_RULE_PATH_BENEATH, &pb, 0);
        close(fd);
    }

    for (size_t i = 0; i < net_count; i++) {
        struct landlock_net_port_attr nb = {
            .allowed_access = nets[i].access & handled_net,
            .port = nets[i].port,
        };
        if (nb.allowed_access)
            landlock_add_rule(rs, LANDLOCK_RULE_NET_PORT, &nb, 0);
    }

    if (prctl(PR_SET_NO_NEW_PRIVS, 1, 0, 0, 0) < 0)
        return -1;
    if (landlock_restrict_self(rs, LANDLOCK_RESTRICT_SELF_TSYNC) < 0)
        return -1;
    close(rs);
    return 1;
}

static int apply_seccomp(void) {
    if (seccomp_profile == 1)
        return 0;
#ifdef USE_SECCOMP
    scmp_filter_ctx ctx = seccomp_init(SCMP_ACT_ALLOW);
    if (!ctx)
        return -1;
    const int deny[] = {
        SCMP_SYS(mount), SCMP_SYS(umount2), SCMP_SYS(pivot_root), SCMP_SYS(chroot),
        SCMP_SYS(unshare), SCMP_SYS(setns), SCMP_SYS(ptrace), SCMP_SYS(kexec_load),
        SCMP_SYS(init_module), SCMP_SYS(finit_module), SCMP_SYS(delete_module),
        SCMP_SYS(reboot), SCMP_SYS(bpf), SCMP_SYS(perf_event_open),
        SCMP_SYS(process_vm_readv), SCMP_SYS(process_vm_writev),
    };
    if (seccomp_profile == 2) {
        const int strict[] = { SCMP_SYS(execve), SCMP_SYS(execveat) };
        for (size_t i = 0; i < sizeof(strict) / sizeof(strict[0]); i++)
            seccomp_rule_add(ctx, SCMP_ACT_ERRNO(EPERM), strict[i], 0);
    }
    for (size_t i = 0; i < sizeof(deny) / sizeof(deny[0]); i++)
        seccomp_rule_add(ctx, SCMP_ACT_ERRNO(EPERM), deny[i], 0);
    if (seccomp_load(ctx) < 0) {
        seccomp_release(ctx);
        return -1;
    }
    seccomp_release(ctx);
    return 1;
#else
    return 0;
#endif
}

static int self_check(void) {
    int abi = landlock_create_ruleset(NULL, 0, LANDLOCK_CREATE_RULESET_VERSION);
    if (abi >= 0)
        printf("landlock_abi=%d\n", abi);
    else
        printf("landlock_abi=0\n");
#ifdef USE_SECCOMP
    printf("seccomp=1\n");
#else
    printf("seccomp=0\n");
#endif
    return 0;
}

int main(int argc, char **argv) {
    const char *policy_path = NULL;
    bool stdio_passthrough = false;
    int sep = -1;
    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--self-check") == 0)
            return self_check();
        if (strcmp(argv[i], "--policy") == 0 && i + 1 < argc)
            policy_path = argv[++i];
        else if (strcmp(argv[i], "--stdio-passthrough") == 0)
            stdio_passthrough = true;
        else if (strcmp(argv[i], "--") == 0) {
            sep = i + 1;
            break;
        }
    }
    if (sep < 0 || sep >= argc) {
        fprintf(stderr, "usage: relic-sandbox --policy <file> [--stdio-passthrough] -- <exe> [args...]\n");
        return 2;
    }
    if (policy_path && parse_policy(policy_path) == 0) {
        apply_landlock();
        apply_seccomp();
    }

    (void)stdio_passthrough;
    execvp(argv[sep], &argv[sep]);
    fprintf(stderr, "execvp failed: %s\n", strerror(errno));
    return 127;
}
