#include <stdio.h>
#include <stdlib.h>

#include "lcms2.h"

int main(int argc, char **argv)
{
    if (argc != 6) {
        fprintf(stderr, "usage: lcms_compare <source.icc> <destination.icc> <r> <g> <b>\n");
        return 2;
    }

    cmsHPROFILE source = cmsOpenProfileFromFile(argv[1], "r");
    cmsHPROFILE destination = cmsOpenProfileFromFile(argv[2], "r");
    if (source == NULL || destination == NULL) {
        fprintf(stderr, "profile open failed\n");
        if (source != NULL) cmsCloseProfile(source);
        if (destination != NULL) cmsCloseProfile(destination);
        return 3;
    }

    cmsHTRANSFORM transform = cmsCreateTransform(
        source,
        TYPE_RGB_8,
        destination,
        TYPE_RGB_8,
        INTENT_RELATIVE_COLORIMETRIC,
        0);
    if (transform == NULL) {
        fprintf(stderr, "transform creation failed\n");
        cmsCloseProfile(source);
        cmsCloseProfile(destination);
        return 4;
    }

    unsigned char input[3] = {
        (unsigned char)strtoul(argv[3], NULL, 10),
        (unsigned char)strtoul(argv[4], NULL, 10),
        (unsigned char)strtoul(argv[5], NULL, 10)
    };
    unsigned char output[3] = { 0, 0, 0 };
    cmsDoTransform(transform, input, output, 1);
    printf("%u,%u,%u\n", output[0], output[1], output[2]);

    cmsDeleteTransform(transform);
    cmsCloseProfile(source);
    cmsCloseProfile(destination);
    return 0;
}
