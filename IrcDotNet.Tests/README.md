Running Tests
=============

The tests in this project may be run under the Visual Studio Unit Testing
Framework.

All tests in this project presume the availability of a fully-functional and
compliant IRC server. The server to which `irc.freenode.net` resolves is used by
default.

## Important

Do not run any individual tests in this project, as most will fail because they
are not begun in the required state. You should only run the `CompleteIrcClient`
ordered test, which is designed to execute the unit tests in a fixed order with
pre-defined conditions (expected states) and timeouts.

## Note

When running in debug mode, tests never time out.

## Note

In rare situations, the `CompleteIrcClient` test may fail despite the library
operating correctly in every way. This could be due to a number of reasons, most
likely that an element of the server is not functioning properly, or extreme lag
on the client or server end (though the timeouts used should be sufficient to
accommodate for such delays). If after verifying that the IRC server/network
used for the tests is operating as it should, and a particular unit test has
failed on several occasions, only then should it be deemed a likely bug.
