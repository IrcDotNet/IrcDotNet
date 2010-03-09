Instructions for Running Tests
==============================

IMPORTANT:
Do not run any individual tests in this project, as most will fail because they are not begun in the required state.
You should only run the CompleteIrcClient ordered test, which is designed to execute the unit tests in a fixed order
with pre-defined conditions (expected states) and timeouts.

NOTE:
In rare situations, the CompleteIrcClient test may fail despite the library operating correctly in every way. This could
be due to a number of reasons, most likely elements of the server not functioning properly, or extreme lag on the client
or server end (though the timeouts used should be sufficient to accomodate for such delays). If after verifying that the
IRC server/network used for the tests is operating as it should, and a particular unit test has failed on several
occassions, only then should it be deemd a likely bug.
