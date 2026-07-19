1. Run

`docker-compose up -d --build
`
Check that you have two containers running. One for the app and one for a simple Postgres DB (yes I chose a persistence layer)

![img.png](.\Documentation\docker-containers.png)

2. Go to:

http://localhost:8080/scalar/v1

in order to test endpoints using Scalar UI integration.

## Endpoint Testing in Scalar

### POST /groups/{groupId}/floor

**Summary:** Obtain the Floor (Push to Talk)

**Description:** Allows a user to request and obtain the "floor" for a specified radio group. Only one user can hold the floor at a time.

Example:
```
POST http://localhost:8080/groups/group1/floor
Content-Type: application/json

{
  "userId": "user1"
}
```

To test for 200 OK response, try access the floor by user from group1.


![post-testing](.\Documentation\image.png)

To test for 409 Conflict, try access the floor with a user from a different group. (group1, user2).

![alt text](.\Documentation\image-1.png)

Connect to the DB container (port: 5432) (use DBeaver as GUI). Check the changes you make in the Postgres DB :)
![alt text](.\Documentation\image-db.png)
### Explanation:
The two IDs are independent — there's no built-in pairing between "group2" and "user2". Think of it like a radio channel:

- groupId = which radio channel/talkgroup (e.g. group1, dispatch, channel-3)
- userId = which person is talking

When you sent groupId=group2, userId=user2, that was a request for group2's floor — which had never been requested before, so there was no holder yet -> naturally Obtained. Nothing to conflict with.

To actually reproduce a conflict, both requests must target the exact same groupId, with two different userIds, and the second one must arrive before the first user releases (or before any timeout, if that's implemented).

### DELETE /groups/{groupId}/floor/{userId}

**Summary:** Release the Floor

**Description:** Allows a user to release the floor they are holding for a specified group.

Example:
```
DELETE http://localhost:8080/groups/group1/floor/user1
```